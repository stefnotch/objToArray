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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileTest
{
	class Program
	{
		public static void Main(string[] args)
		{
			//Dir
			if (Directory.Exists("./obj"))
			{
				Console.WriteLine("Directory Found");

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
				if (File.Exists(fileName + ".obj") && File.Exists(fileName + ".mtl"))
				{
					Console.WriteLine("Files found, starting to read them");

					try { File.Delete("./obj/output.txt"); }
					catch (Exception e)
					{
						printError("No file to delete, ignore this error" + e);
					}
					//Create the output file
					StreamWriter JSONFile = File.CreateText("./obj/output.txt");

					List<string> vertices = new List<string>();
					List<string> uvs = new List<string>();
					List<string> normals = new List<string>();

					List<string> texNames = new List<string>();
					List<int[]> faces = new List<int[]>();

					List<int[]> bones = new List<int[]>();
					List<string> parts = new List<string>();

					//Does it have UVs
					bool hasUvs = true;
					//Length of the triangle. (1=>only vertices, 2=>vertices and uvs, 3=>vertices, uvs, and normals)
					int length = 0;
					#region OBJ READER
					using (StreamReader objReader = new StreamReader(fileName + ".obj"))
					{
						int boneIndex = 0;
						string currentLine;
						while ((currentLine = objReader.ReadLine()) != null)
						{
							//Just making it a bit foolproof
							currentLine = currentLine.Trim();
							if (currentLine.Length == 0)
							{
								//printError("Line length = 0");
							}
							else if (currentLine[0] == '#' || currentLine[0] == 's' || currentLine[0] == 'o' || currentLine.StartsWith("mtllib"))
							{
								//Do nothing
							}
							else if (currentLine.StartsWith("usemtl"))
							{
								faces.Add(new int[] { texNames.Count });
								bones.Add(new int[0]);//Dummy
								texNames.Add("new" + currentLine.Substring(3));
							}
							else if (currentLine.StartsWith("vt"))
							{
								string[] uv = currentLine.Substring(2).Trim(' ').Split(' ');
								//Flip the y coordinate of the UV
								if (uv.Length > 2)
								{
									printError("Too long UVs: " + currentLine);
								}
								uv[1] = (1 - Double.Parse(uv[1])).ToString();
								uvs.Add(String.Join(",", uv));
							}
							else if (currentLine.StartsWith("vn"))
							{
								normals.Add(currentLine.Substring(2).Trim(' ').Replace(' ', ','));
							}
							else if (currentLine.StartsWith("v"))
							{
								vertices.Add(currentLine.Substring(1).Trim(' ').Replace(' ', ','));
							}
							//TODO Magically add missing parts (Index: -1)
							//TODO Not triangle face check
							else if (currentLine.StartsWith("f"))
							{
								string[] currentFace = currentLine.Substring(1).Trim(' ').Split(' ', '/');
								if (currentFace.Length != length)
								{
									if (length == 0)
									{
										length = currentFace.Length;
									}
									else
									{
										printError("Inconsistent face lengths: " + currentLine + length);
									}
								}
								//The array that will get added to the faces (+1 for the bone index)
								int[] addToFaces = new int[currentFace.Length];
								//Loop over each index
								for (int i = 0; i < currentFace.Length; i++)
								{
									//If it doesn't have any UVs..
									if (currentFace.Length == 9 && i % 3 == 1 && currentFace[i].Length == 0)
									{
										hasUvs = false;
									}
									else if (!hasUvs)
									{
										printError("Inconsistent uvs. The model sometimes has them, sometimes it doesn't!");
									}

									//OBJ indices start at one
									addToFaces[i] = Int32.Parse(currentFace[i]) - 1;

								}
								faces.Add(addToFaces);
								bones.Add(new int[3] { boneIndex, boneIndex, boneIndex });
							}
							else if (currentLine[0] == 'g') //obj group
							{
								string groupName = currentLine.Substring(1).Trim(' ').Replace(' ', ',');
								if (groupName == "(null)")
								{
									groupName = "notYetDefined";
									printError("Everything needs to be part of a vertex group, non-fatal error");
								}
								//Bone, another attribute for all vertices!
								int groupIndex = parts.IndexOf(groupName);
								if (groupIndex > -1)
								{
									boneIndex = groupIndex;
								}
								else
								{
									boneIndex = parts.Count;
									parts.Add(groupName);
								}

							}
							else
							{
								Console.WriteLine(currentLine);
								System.Threading.Thread.Sleep(500);
							}
						}
					}
					#endregion
					#region MTL READER
					Console.WriteLine("MTL: ");
					Dictionary<string, string> images = new Dictionary<string, string>();
					using (StreamReader mtlReader = new StreamReader(fileName + ".mtl"))
					{
						//Read the .mtl file
						string currentLine;
						string currMTL = "nonexistent";
						while ((currentLine = mtlReader.ReadLine()) != null)
						{
							Console.WriteLine("CL: " + currentLine);
							if (currentLine.IndexOf("newmtl") > -1)
							{
								currMTL = currentLine;
							}
							else if (currentLine.IndexOf("map_") > -1) //TODO make this better
							{
								if (currentLine.IndexOf(".tga") > -1)
								{
									images.Add(currMTL, System.Text.RegularExpressions.Regex.Replace(currentLine, "map_.*? ", "").Replace("\\", "/").Replace("tga", "png").Trim());
									printError("CONVERT TO PNG!");
								}
								else {
									if (currentLine.Trim().Split(' ').Length > 1)
										//If it doesn't already containt that key
										if (!images.ContainsKey(currMTL))
											images.Add(currMTL, currentLine.Replace("map_Kd", "").Replace("\\", "/").Trim());
								}
							}
						}
					}
					#endregion

					//TODO Fix the empty newmtl
					//Divide the length by 3
					length = length / 3;

					#region Face normals
					double[][] faceNormals = new double[faces.Count][];
					for (int j = 0; j < faces.Count; j++)
					{
						if (faces[j].Length > 2)
						{
							int[] currFace = faces[j];
							//currFace[0*length] currFace[1 * length ] currFace[2*length]
							double[] verts = toVertices(vertices[currFace[0 * length]], vertices[currFace[1 * length]], vertices[currFace[2 * length]]);

							/*The cross product of two sides of the triangle equals the surface normal.
	  So, if V = P2 - P1 and W = P3 - P1, and N is the surface normal, then:
      Nx = (Vy * Wz)−(Vz * Wy)
	  Ny = (Vz * Wx)−(Vx * Wz)
	  Nz = (Vx * Wy)−(Vy * Wx)
*/
							double Vx = verts[3] - verts[0];
							double Vy = verts[4] - verts[1];
							double Vz = verts[5] - verts[2];

							double Wx = verts[6] - verts[0];
							double Wy = verts[7] - verts[1];
							double Wz = verts[8] - verts[2];

							//Face Normals
							double Nx = (Vy * Wz) - (Vz * Wy);
							double Ny = (Vz * Wx) - (Vx * Wz);
							double Nz = (Vx * Wy) - (Vy * Wx);
							//Normals
							faceNormals[j] = normalize(Nx, Ny, Nz);
						}
					}
					#endregion

					#region Bary coords and bones
					var toLookup = new List<KeyValuePair<string, triAndVertIndex>>();
					for (int j = faces.Count - 1; j >= 0; j--)
					{
						if (faces[j].Length > 2)
						{
							for (var vert = 0; vert < 3; vert++)
							{
								toLookup.Add(new KeyValuePair<string, triAndVertIndex>(vertices[faces[j][vert * length]], new triAndVertIndex { triIndex = j, vertIndex = vert }));
							}
						}
					}
					double[,] bary = new double[faces.Count, 3]; //Filled with: default( int )
					const double threshold = 0.6;
					const double addTo = 1 - threshold;
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
							foreach (triAndVertIndex index in matchingVertices0)
							{
								//Lesser faces will expand
								//Oh, yeah! It's working! (Magic!)
								if (bones[j][0] != bones[index.triIndex][index.vertIndex])
								{
									bones[j][0] = -1;
								}
								foreach(triAndVertIndex otherIndex in matchingVertices1)
								{
									if(otherIndex.triIndex == index.triIndex)
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
							foreach (triAndVertIndex index in matchingVertices1)
							{
								//TODO single matching point
								//Lesser faces will expand
								if (bones[j][1] != bones[index.triIndex][index.vertIndex])
								{
									bones[j][1] = -1;
								}
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
							foreach (triAndVertIndex index in matchingVertices2)
							{
								//Lesser faces will expand
								if (bones[j][2] != bones[index.triIndex][index.vertIndex])
								{
									bones[j][2] = -1;
								}
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

						}
						//TODO Single matching points:						
					}
					#endregion


					//Write to file
					JSONFile.Write("model = [");
					bool firstTime = true;
					for (int j = 0; j < faces.Count; j++)
					{
						int[] currFace = faces[j];
						if (currFace.Length == 1)
						{
							string image;
							if (!images.ContainsKey(texNames[currFace[0]]))
							{
								image = "nonexistent.png";
							}
							else {
								//Aww, yeah! That's what I call awesome code! 
								image = images[texNames[currFace[0]]];
							}
							Console.WriteLine(image);
							if (firstTime)
							{
								JSONFile.Write("[");
								firstTime = false;
							}
							else {
								JSONFile.Write("],\n[");
							}
							JSONFile.Write('"' + image + "\",");
						}
						else
						{
							string[] baryCoordsOfTri = toBary(bary[j, 0], bary[j, 1], bary[j, 2]);
							int prevBone = bones[j][0];
							//Triangle
							for (int i = 0; i < 3; i++)
							{
								JSONFile.Write(vertices[currFace[i * length]]);


								if (length == 2)
								{
									JSONFile.Write("," + uvs[currFace[i * length + 1]]);
								}
								else if (length == 3)
								{
									if (hasUvs)
									{
										JSONFile.Write("," + uvs[currFace[i * length + 1]]);
									}
									JSONFile.Write("," + normals[currFace[i * length + 2]]);
								}

								JSONFile.Write(baryCoordsOfTri[i]);
								if (prevBone != bones[j][i])
								{
									printError("J: " + j + " prevBon: " + prevBone + " dfgds: " + bones[j][i]);
									prevBone = bones[j][i];
								}
								JSONFile.Write("," + bones[j][i]);

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
					for (int i = 0; i < parts.Count; i++)
					{
						//pitch/yaw or something else..?
						// ",pos:[0,0,0],pitch:0,yaw:0},"
						bonesFile.Write("{name:\"" + parts[i] + "\",index:" + i + "},");
					}

					bonesFile.Write("];");
					bonesFile.Close();
					//TODO add info about how the model is structured
					Console.WriteLine("Vertex count: " + vertices.Count);
					Console.WriteLine("UVs count: " + uvs.Count);
					Console.WriteLine("Normals count: " + normals.Count);
					Console.WriteLine("Face count: " + faces.Count);
					Console.WriteLine("Length: " + length);



					try { File.Delete("./obj/output.js"); } catch (Exception e) { };
					try { File.Delete("./obj/outputBones.js"); } catch (Exception e) { };
					File.Move("./obj/output.txt", Path.ChangeExtension("./obj/output.txt", ".js"));
					File.Move("./obj/outputBones.txt", Path.ChangeExtension("./obj/outputBones.txt", ".js"));
				}
				else
				{
					printError("Obj files don't exist");
				}

			}
			else
			{
				printError("Creating Dir");
				Directory.CreateDirectory("./obj");
			}


			Console.WriteLine("DONE!");
			Console.Read();
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

		static double[] toVertices(params string[] toConvert)
		{
			return Array.ConvertAll(string.Join(",", toConvert).Split(','), l => double.Parse(l)); //Lambda!
		}

		static double[] normalize(double x, double y, double z)
		{
			double length = Math.Sqrt(x * x + y * y + z * z);
			// make sure we don't divide by 0.
			if (length > 0.00001)
			{
				return new double[] { x / length, y / length, z / length };
			}
			else {
				return new double[] { 0, 0, 0 };
			}
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

		//TODO needs to be changed to account for points 
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
	}

	struct triAndVertIndex
	{
		public int triIndex;
		public int vertIndex;
	}

}


