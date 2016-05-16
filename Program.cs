/*--------------------------------------------------------------
 *		HTBLA-Leonding / Class: 1CHIF
 *--------------------------------------------------------------
 *              Stefan Brandmair
 *--------------------------------------------------------------
 * Description:
 * Turns an obj + mtl file into something that I can use
 *--------------------------------------------------------------
*/

//http://web.cse.ohio-state.edu/~hwshen/581/Site/Lab3_files/Labhelp_Obj_parser.htm
using Assimp;
using Assimp.Configs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FileTest
{
	class Program
	{
		/// <summary>
		/// Vertices, 1 element --> new material/texture INT TEXTURECOUNT = 0; for the for loop
		/// </summary>
		static List<Vector3D[]> vertices = new List<Vector3D[]>();
		static List<Vector3D[]> uvs = new List<Vector3D[]>();
		static List<Vector3D[]> normals = new List<Vector3D[]>();
		/// <summary>
		/// Texture names, if the vertices have only 1 element, a new texture will be used
		/// </summary>
		static List<string> texNames = new List<string>();
		/// <summary>
		/// Face normals, crease threshold: 134.43
		/// </summary>
		static List<Vector3D> faceNormals;

		static List<KeyValuePair<Vector3D, triAndVertIndex>> toLookup;
		//Vector3D implements GetHashCode
		static Lookup<Vector3D, triAndVertIndex> adjVert;
		//Each vertex has a bone
		//Bone ID (Integers that store the bone's ID for each vertex)
		static List<int[]> bones;
		static List<string> boneNames;
		static Scene scene;
		public static void Main(string[] args)
		{
			//Dir
			if (Directory.Exists("./obj"))
			{
				Console.WriteLine("Directory Found");
			}
			else
			{
				printError("Creating Dir");
				Directory.CreateDirectory("./obj");
			}

			//File Input
			string fileName = "./obj/" + getUserInput("OBJ / MTL file name");

			if (fileName == "./obj/")
			{
				string[] possibleFiles = Directory.GetFiles("./obj");
				foreach (string currFileName in possibleFiles)
				{
					if (currFileName.EndsWith(".obj") && File.Exists(currFileName.Replace(".obj", ".mtl")))
					{
						fileName = currFileName;
						break;
					}
				}
			}

			fileName = fileName.Replace(".mtl", "").Replace(".obj", "");


			//If it exists, read it
			if (!File.Exists(fileName + ".obj") || !File.Exists(fileName + ".mtl"))
			{
				printError("Obj file doesn't exist");
				return;
			}
			Console.WriteLine("Files found, starting to read them");

			try { File.Delete("./obj/output.txt"); }
			catch (Exception e)
			{
				printError("No file to delete, ignore this error" + e);
			}
			//Create a new importer
			AssimpContext importer = new AssimpContext();
			importer.SetConfig(new IFCUseCustomTriangulationConfig(true));
			//This is how we add a configuration (each config is its own class)
			//NormalSmoothingAngleConfig config = new NormalSmoothingAngleConfig(66.0f);
			//importer.SetConfig(config);

			//This is how we add a logging callback 
			LogStream logstream = new LogStream(delegate (String msg, String userData)
			{
				Console.WriteLine(msg);
			});
			logstream.Attach();

			//Import the model. All configs are set. The model
			//is imported, loaded into managed memory. Then the unmanaged memory is released, and everything is reset.
			//Triangulating is already being done
			//TODO aiProcess_JoinIdenticalVertices (Index buffer objects)
			scene = importer.ImportFile(fileName + ".obj", PostProcessPreset.TargetRealTimeMaximumQuality | PostProcessSteps.FlipUVs | PostProcessSteps.OptimizeMeshes);
			parseNode(scene.RootNode);
			//End of example
			importer.Dispose();
			adjVert = (Lookup<Vector3D, triAndVertIndex>)toLookup.ToLookup((item) => item.Key, (item) => item.Value);
			double THRESHOLD = Math.Cos(134.43 * Math.PI/180);

			//Edit
			/*
			#region Bary coords and bones
			
			double[,] bary = new double[faces.Count, 3]; //Filled with: default( int )
			
			const double addTo = 1 - THRESHOLD;
			Lookup<string, triAndVertIndex> adj = (Lookup<string, triAndVertIndex>)toLookup.ToLookup((item) => item.Key, (item) => item.Value);
			//Each face/triangle has 3 bary coords
			//Lines:
			for (int j = faces.Count - 1; j >= 0; j--)
			{
				if (faces[j].Length > 2)
				{
					IEnumerable<triAndVertIndex> matchingVertices0 = adj[vertices[faces[j][0]]];
					IEnumerable<triAndVertIndex> matchingVertices1 = adj[vertices[faces[j][length]]];
					IEnumerable<triAndVertIndex> matchingVertices2 = adj[vertices[faces[j][2 * length]]];
					//2 Matching points
					//TriIndex = triangle index of the adjacent triangle
					bool noAdjVerts = true;
					foreach (triAndVertIndex index in matchingVertices0)
					{
						noAdjVerts = false;
						//Oh, yeah! It's working! (Magic!)
						singleMatchingVertex(0, j, index);

						foreach (triAndVertIndex otherIndex in matchingVertices1)
						{
							if (otherIndex.triIndex == index.triIndex)
							{
								double angleBetweenTriangles = Math.Abs(dotProcuct(faceNormals[j], faceNormals[otherIndex.triIndex]));
								if (angleBetweenTriangles < threshold)
								{
									bary[j, 2] = 1;// angleBetweenTriangles + addTo;
								}
								break;
							}
						}
					}
					if (noAdjVerts)
					{
						bary[j, 1] = 1;
						bary[j, 2] = 1;
					}
					noAdjVerts = true;
					foreach (triAndVertIndex index in matchingVertices1)
					{
						noAdjVerts = false;
						singleMatchingVertex(1, j, index);

						foreach (triAndVertIndex otherIndex in matchingVertices2)
						{
							if (otherIndex.triIndex == index.triIndex)
							{
								double angleBetweenTriangles = Math.Abs(dotProcuct(faceNormals[j], faceNormals[otherIndex.triIndex]));
								if (angleBetweenTriangles < threshold)
								{
									bary[j, 0] = 1;// TODO angleBetweenTriangles + addTo;
								}
								break;
							}
						}
					}
					if (noAdjVerts)
					{
						bary[j, 0] = 1;
						bary[j, 2] = 1;
					}
					noAdjVerts = true;
					foreach (triAndVertIndex index in matchingVertices2)
					{
						noAdjVerts = false;
						singleMatchingVertex(2, j, index);
						foreach (triAndVertIndex otherIndex in matchingVertices0)
						{
							if (otherIndex.triIndex == index.triIndex)
							{
								double angleBetweenTriangles = Math.Abs(dotProcuct(faceNormals[j], faceNormals[otherIndex.triIndex]));
								if (angleBetweenTriangles < threshold)
								{
									bary[j, 1] = 1;// TODO angleBetweenTriangles + addTo;
								}
								break;
							}
						}
					}
					if (noAdjVerts)
					{
						bary[j, 0] = 1;
						bary[j, 1] = 1;
					}

				}
				//TODO Single matching points:						
			}
			#endregion
			*/

			//Create the output file
			StreamWriter JSONFile = File.CreateText("./obj/output.txt");
			//Write to file
			JSONFile.Write("model = [");
			bool firstTime = true;
			for (int j = 0, texCount = 0; j < vertices.Count; j++)
			{
				Vector3D[] currVert = Program.vertices[j];
				if (currVert.Length == 1)
				{

					if (firstTime)
					{
						JSONFile.Write("[");
						firstTime = false;
					}
					else
					{
						JSONFile.Write("],\n[");
					}
					JSONFile.Write('"' + texNames[(int)currVert[0].X] + "\",");
					texCount++;
				}
				else
				{
					//Edit
					string[] baryCoordsOfTri = new string[0];//toBary(bary[j, 0], bary[j, 1], bary[j, 2]);
					//Triangle
					for (int i = 0; i < 3; i++)
					{
						JSONFile.Write(Vec3DToString(currVert[i]));
						JSONFile.Write(UVToString(Program.uvs[j - texCount][i]));
						JSONFile.Write(Vec3DToString(Program.normals[j - texCount][i]));

						JSONFile.Write(baryCoordsOfTri[i]);
						JSONFile.Write("," + bones[j - texCount][i]);

						//Pawsome JS is perfectly capable of realizing that the trailing commas aren't part of the array!
						JSONFile.Write(',');
					}
				}
			}


			JSONFile.Write("]];");
			JSONFile.Close();


			StreamWriter bonesFile = File.CreateText("./obj/outputBones.txt");
			//You are going to have to reorder the parts manually
			bonesFile.Write("\nbones = [");
			for (int i = 0; i < boneNames.Count; i++)
			{
				//pitch/yaw or something else..?
				// ",pos:[0,0,0],pitch:0,yaw:0},"
				bonesFile.WriteLine("{name:\"" + boneNames[i] + "\",index:" + i + ",parent:-1},");
			}

			bonesFile.Write("];");
			bonesFile.Close();

			try { File.Delete("./obj/output.js"); } catch (Exception) { };
			try { File.Delete("./obj/outputBones.js"); } catch (Exception) { };
			File.Move("./obj/output.txt", Path.ChangeExtension("./obj/output.txt", ".js"));
			File.Move("./obj/outputBones.txt", Path.ChangeExtension("./obj/outputBones.txt", ".js"));


			Console.WriteLine("DONE!");
			Console.Read();
		}

		static string prevMaterial = "";
		static void parseNode(Node currentNode)
		{
			if (currentNode.HasMeshes)
			{
				//Console.Write(currentNode.Name);
				foreach (int index in currentNode.MeshIndices)
				{
					Mesh mesh = scene.Meshes[index];
					//mesh.MaterialIndex
					//scene.Materials
					//Console.WriteLine("Material: " + scene.Materials[mesh.MaterialIndex].Name);
					if (mesh.HasNormals && mesh.TextureCoordinateChannelCount > 0)
					{
						if (prevMaterial != scene.Materials[mesh.MaterialIndex].Name)
						{
							prevMaterial = scene.Materials[mesh.MaterialIndex].Name;
							vertices.Add(new Vector3D[1] { new Vector3D(texNames.Count, 0, 0) });
							int numberOfTextures = scene.Materials[mesh.MaterialIndex].GetAllMaterialTextures().Length;
							string texFilePath;
							if (numberOfTextures > 0)
							{
								texFilePath = scene.Materials[mesh.MaterialIndex].GetAllMaterialTextures()[0].FilePath;
								if (numberOfTextures > 1)
								{
									//TODO
									foreach (TextureSlot tex in scene.Materials[mesh.MaterialIndex].GetAllMaterialTextures())
									{
										if (texFilePath != tex.FilePath)
										{
											printError("TEX Name: " + tex.FilePath);
											printError("more than one texture");
											Debugger.Break();
										}
									}
								}
							}
							else
							{
								printError("No texture");
								//No texture, TODO
								texFilePath = "nonexistent.png";
							}

							string color = ",1,1,1";
							if (scene.Materials[mesh.MaterialIndex].HasColorAmbient)
							{
								color = "," + scene.Materials[mesh.MaterialIndex].ColorAmbient.R + "," + scene.Materials[mesh.MaterialIndex].ColorAmbient.G + "," + scene.Materials[mesh.MaterialIndex].ColorAmbient.B;
							}
							else if (scene.Materials[mesh.MaterialIndex].HasColorDiffuse)
							{
								color = "," + scene.Materials[mesh.MaterialIndex].ColorDiffuse.R + "," + scene.Materials[mesh.MaterialIndex].ColorDiffuse.G + "," + scene.Materials[mesh.MaterialIndex].ColorDiffuse.B;
							}
							texNames.Add(texFilePath + color);

						}
						//Console.WriteLine(mesh.Vertices.Count + ":" + mesh.Normals.Count + ":" + mesh.VertexCount + ":" + mesh.TextureCoordinateChannels[0].Count);
						foreach (Face face in mesh.Faces)
						{
							Debug.Assert(face.IndexCount == 3, "Not triangulated face");

							Vector3D[] points = new Vector3D[] { mesh.Vertices[face.Indices[0]], mesh.Vertices[face.Indices[1]], mesh.Vertices[face.Indices[2]] };
							vertices.Add(points);
							//Vector3Ds with z = 0
							uvs.Add(new Vector3D[] { mesh.TextureCoordinateChannels[0][face.Indices[0]], mesh.TextureCoordinateChannels[0][face.Indices[1]], mesh.TextureCoordinateChannels[0][face.Indices[2]] });
							normals.Add(new Vector3D[] { mesh.Normals[face.Indices[0]], mesh.Normals[face.Indices[1]], mesh.Normals[face.Indices[2]] });

							Vector3D U = points[1] - points[0];
							Vector3D V = points[2] - points[0];
							Vector3D faceNormal = Vector3D.Cross(U, V);
							faceNormal.Normalize();
							
							//TODO make coordinates less precise
							/*
							toLookup.Add(
								new KeyValuePair<Vector3D, triAndVertIndex>(points[0], new triAndVertIndex { triIndex = face.Indices[0], vertIndex = 0 }));
							toLookup.Add(
								new KeyValuePair<Vector3D, triAndVertIndex>(points[1], new triAndVertIndex { triIndex = face.Indices[1], vertIndex = 1 }));
							toLookup.Add(
								new KeyValuePair<Vector3D, triAndVertIndex>(points[2], new triAndVertIndex { triIndex = face.Indices[2], vertIndex = 2 }));
								*/

							toLookup.Add(
									new KeyValuePair<Vector3D, triAndVertIndex>(points[0], new triAndVertIndex { triIndex = faceNormals.Count, vertIndex = 0 }));
							toLookup.Add(
								new KeyValuePair<Vector3D, triAndVertIndex>(points[1], new triAndVertIndex { triIndex = faceNormals.Count, vertIndex = 1 }));
							toLookup.Add(
								new KeyValuePair<Vector3D, triAndVertIndex>(points[2], new triAndVertIndex { triIndex = faceNormals.Count, vertIndex = 2 }));
							faceNormals.Add(faceNormal);
						}
					}
				}
			}

			//Parse all the other nodes
			for (int i = 0; i < currentNode.ChildCount; i++)
			{
				parseNode(currentNode.Children[i]);
			}
		}

		/// <summary>
		/// Gets some user input
		/// </summary>
		/// <param name="text">The message</param>
		static string getUserInput(string text)
		{
			Console.Write(text + ": ");
			return Console.ReadLine();
		}

		/// <summary>
		/// Prints an error message
		/// </summary>
		/// <param name="errorMessage"></param>
		static void printError(string errorMessage)
		{
			ConsoleColor previousColor = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.WriteLine(errorMessage);
			Console.ForegroundColor = previousColor;
		}

		static double dotProcuct(double[] array1, double[] array2)
		{

			if (array1.Length != array2.Length)
			{
				printError("Dotproduct: arrays with different lengths");
			}
			//Multiply each element of array2 by array1[i] and get their sum
			double sum = 0;
			for (int i = 0; i < array1.Length; i++)
			{
				sum += array1[i] * array2[i];
			}
			return sum;

		}

		//TODO needs to be changed to account for single points 
		/// <summary>
		/// Turns a bunch of random numbers into valid barycentric coordinates
		/// </summary>
		/// <param name="one"></param>
		/// <param name="two"></param>
		/// <param name="three"></param>
		/// <returns></returns>
		static string[] toBary(double one, double two, double three)
		{
			double[] bary = {
			1,1,1,
			1,1,1,
			1,1,1};
			if (one > 0)
			{
				bary[1] = one;
				bary[3] = 0;
				bary[6] = 0;
			}

			if (two > 0)
			{
				bary[1] = 0;
				bary[4] = two;
				bary[7] = 0;
			}

			if (three > 0)
			{
				bary[2] = 0;
				bary[5] = 0;
				bary[7] = three;
			}
			string[] baryString = new string[3];
			for (int i = 0, c = 0; i < 9; i += 3, c++)
			{
				baryString[c] = "," + bary[i] + "," + bary[i + 1] + "," + bary[i + 2];
			}
			return baryString;
		}
		static string Vec3DToString(Vector3D vec)
		{
			return "," + vec.X + "," + vec.Y + "," + vec.Z;
		}

		static string UVToString(Vector3D vec)
		{
			return "," + vec.X + "," + vec.Y;
		}
		static void singleMatchingVertex(int vertexIndex, int j, triAndVertIndex otherIndex)
		{
			if (bones[j][vertexIndex] != bones[otherIndex.triIndex][otherIndex.vertIndex])
			{
				char boneStartLetter = boneNames[bones[j][vertexIndex]][0];
				if (boneStartLetter == 'e')
				{
					bones[otherIndex.triIndex][otherIndex.vertIndex] = bones[j][0];
				}
				else if (boneStartLetter == 'c')
				{
					bones[j][vertexIndex] = bones[otherIndex.triIndex][otherIndex.vertIndex];
				}
				//Lesser faces will contract
				else if (bones[j][vertexIndex] < bones[otherIndex.triIndex][otherIndex.vertIndex])
				{
					bones[j][vertexIndex] = bones[otherIndex.triIndex][otherIndex.vertIndex];
				}
			}
		}
	}
	struct triAndVertIndex
	{
		public int triIndex;
		public int vertIndex;
	}

}


