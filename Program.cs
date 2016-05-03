﻿/*--------------------------------------------------------------
 *		HTBLA-Leonding / Class: 1CHIF
 *--------------------------------------------------------------
 *              Stefan Brandmair
 *--------------------------------------------------------------
 * Description:
 * Turns an obj + mtl file into something that I can use
 *--------------------------------------------------------------
*/
//TODO If the vertex doesn't have an adjacent vertex on any of the edges, it won't work as intended. That needs to be fixed.
//I don't think that is an issue anymore
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

					List<string> parts = new List<string>();

					//Does it have UVs
					bool hasUvs = true;
					//Length of the triangle. (3=>only vertices, 6=>vertices and uvs, 9 => vertices, uvs, and normals)
					int length = 0;

					#region OBJ READER
					using (StreamReader objReader = new StreamReader(fileName + ".obj"))
					{
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
								//The array that will get added to the faces
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
									addToFaces[i] = Int32.Parse(currentFace[i]) - 1;
								}

								faces.Add(addToFaces);
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
									faces.Add(new int[] { groupIndex, 0 });
								}
								else
								{
									faces.Add(new int[] { parts.Count, 0 });
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
					//I hope this works
					double[][] faceNormals = new double[faces.Count][];
					JSONFile.Write("var model = [");
					#region Face normals
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
							var Vx = verts[3] - verts[0];
							var Vy = verts[4] - verts[1];
							var Vz = verts[5] - verts[2];

							var Wx = verts[6] - verts[0];
							var Wy = verts[7] - verts[1];
							var Wz = verts[8] - verts[2];

							//Face Normals
							var Nx = (Vy * Wz) - (Vz * Wy);
							var Ny = (Vz * Wx) - (Vx * Wz);
							var Nz = (Vx * Wy) - (Vy * Wx);
							//Normals
							faceNormals[j] = normalize(Nx, Ny, Nz);
						}
					}
					/*
					 function calculateExtendersAndLines() {
  const off = 0.05;
  const threshold = 0.7;
  var faceNormals = [];
  var lines = [];
  if (model["lines"] == undefined) {
    //Face normals
    //Loop over all triangles
    for (var tri = 0; tri < model["vertices"].length; tri += 9) {
      /*
      The cross product of two sides of the triangle equals the surface normal. 
      So, if V = P2 - P1 and W = P3 - P1, and N is the surface normal, then:
      Nx=(Vy*Wz)−(Vz*Wy)
      Ny=(Vz*Wx)−(Vx*Wz)
      Nz=(Vx*Wy)−(Vy*Wx)

					var Vx = model["vertices"][tri + 3] - model["vertices"][tri];
					var Vy = model["vertices"][tri + 4] - model["vertices"][tri + 1];
					var Vz = model["vertices"][tri + 5] - model["vertices"][tri + 2];

					var Wx = model["vertices"][tri + 6] - model["vertices"][tri];
					var Wy = model["vertices"][tri + 7] - model["vertices"][tri + 1];
					var Wz = model["vertices"][tri + 8] - model["vertices"][tri + 2];

					//Face Normals
					var Nx = (Vy * Wz) - (Vz * Wy);
					var Ny = (Vz * Wx) - (Vx * Wz);
					var Nz = (Vx * Wy) - (Vy * Wx);
					//Normals
					faceNormals.push(MatMath.normalize(Nx, Ny, Nz));

					//MatMath.dotProcuct()
				}

				//Flipped triangles
				for (var tri = 0; tri < model["vertices"].length; tri += 9)
				{
					var adjTriangles = 0;
					for (var triOther = tri + 9; triOther < model["vertices"].length; triOther += 9)
					{
						var touching = false;
						//For each vertex in that triangle
						otherVert:
						for (var vert = 0; vert < 9; vert += 3)
						{
							for (var vertOther = 0; vertOther < 6; vertOther += 3)
							{
								//Check if they are the same point
								if (model.vertices[tri + vert] == model.vertices[triOther + vertOther] &&
								  model.vertices[tri + vert + 1] == model.vertices[triOther + vertOther + 1] &&
								  model.vertices[tri + vert + 2] == model.vertices[triOther + vertOther + 2])
								{
									//One point matches
									//Check if another point matches and make 2 triangles opposite winding orders!
									if ((model.vertices[tri + (vert + 3) % 9] == model.vertices[triOther + (vertOther + 3) % 9] &&
										model.vertices[tri + (vert + 1 + 3) % 9] == model.vertices[triOther + (vertOther + 1 + 3) % 9] &&
										model.vertices[tri + (vert + 2 + 3) % 9] == model.vertices[triOther + (vertOther + 2 + 3) % 9]) ||

									  (model.vertices[tri + (vert + 3) % 9] == model.vertices[triOther + (vertOther + 6) % 9] &&
										model.vertices[tri + (vert + 1 + 3) % 9] == model.vertices[triOther + (vertOther + 1 + 6) % 9] &&
										model.vertices[tri + (vert + 2 + 3) % 9] == model.vertices[triOther + (vertOther + 2 + 6) % 9]))
									{
										if (Math.abs(MatMath.dotProcuct(faceNormals[tri / 9], faceNormals[triOther / 9])) < threshold)
										{

											//Subtract face normals, make them larger!
											//Extenders merge


											lines.push(model.vertices[tri + vert] + faceNormals[triOther / 9][0] * off,
											  model.vertices[tri + vert + 1] + faceNormals[triOther / 9][1] * off,
											  model.vertices[tri + vert + 2] + faceNormals[triOther / 9][2] * off);
											lines.push(model.vertices[tri + (vert + 3) % 9] + faceNormals[tri / 9][0] * off,
											  model.vertices[tri + (vert + 1 + 3) % 9] + faceNormals[tri / 9][1] * off,
											  model.vertices[tri + (vert + 2 + 3) % 9] + faceNormals[tri / 9][2] * off);
											lines.push(model.vertices[tri + (vert + 3) % 9] + faceNormals[triOther / 9][0] * off,
											  model.vertices[tri + (vert + 1 + 3) % 9] + faceNormals[triOther / 9][1] * off,
											  model.vertices[tri + (vert + 2 + 3) % 9] + faceNormals[triOther / 9][2] * off);

											lines.push(model.vertices[tri + (vert + 3) % 9] + faceNormals[tri / 9][0] * off,
											  model.vertices[tri + (vert + 1 + 3) % 9] + faceNormals[tri / 9][1] * off,
											  model.vertices[tri + (vert + 2 + 3) % 9] + faceNormals[tri / 9][2] * off);
											lines.push(model.vertices[tri + vert] + faceNormals[triOther / 9][0] * off,
											  model.vertices[tri + vert + 1] + faceNormals[triOther / 9][1] * off,
											  model.vertices[tri + vert + 2] + faceNormals[triOther / 9][2] * off);
											lines.push(model.vertices[tri + vert] + faceNormals[tri / 9][0] * off,
											  model.vertices[tri + vert + 1] + faceNormals[tri / 9][1] * off,
											  model.vertices[tri + vert + 2] + faceNormals[tri / 9][2] * off);

											touching = true;
										}
										break otherVert;
									}
									if ((model.vertices[tri + (vert + 6) % 9] == model.vertices[triOther + (vertOther + 3) % 9] &&
										model.vertices[tri + (vert + 1 + 6) % 9] == model.vertices[triOther + (vertOther + 1 + 3) % 9] &&
										model.vertices[tri + (vert + 2 + 6) % 9] == model.vertices[triOther + (vertOther + 2 + 3) % 9]) ||

									  (model.vertices[tri + (vert + 6) % 9] == model.vertices[triOther + (vertOther + 6) % 9] &&
										model.vertices[tri + (vert + 1 + 6) % 9] == model.vertices[triOther + (vertOther + 1 + 6) % 9] &&
										model.vertices[tri + (vert + 2 + 6) % 9] == model.vertices[triOther + (vertOther + 2 + 6) % 9]))
									{

										if (Math.abs(MatMath.dotProcuct(faceNormals[tri / 9], faceNormals[triOther / 9])) < threshold)
										{

											//Line triangle one
											lines.push(model.vertices[tri + vert] + faceNormals[tri / 9][0] * off,
											  model.vertices[tri + vert + 1] + faceNormals[tri / 9][1] * off,
											  model.vertices[tri + vert + 2] + faceNormals[tri / 9][2] * off);

											lines.push(model.vertices[tri + (vert + 6) % 9] + faceNormals[triOther / 9][0] * off,
											  model.vertices[tri + (vert + 1 + 6) % 9] + faceNormals[triOther / 9][1] * off,
											  model.vertices[tri + (vert + 2 + 6) % 9] + faceNormals[triOther / 9][2] * off);

											lines.push(model.vertices[tri + (vert + 6) % 9] + faceNormals[tri / 9][0] * off,
											  model.vertices[tri + (vert + 1 + 6) % 9] + faceNormals[tri / 9][1] * off,
											  model.vertices[tri + (vert + 2 + 6) % 9] + faceNormals[tri / 9][2] * off);



											//Line triangle two
											lines.push(model.vertices[tri + (vert + 6) % 9] + faceNormals[triOther / 9][0] * off,
											  model.vertices[tri + (vert + 1 + 6) % 9] + faceNormals[triOther / 9][1] * off,
											  model.vertices[tri + (vert + 2 + 6) % 9] + faceNormals[triOther / 9][2] * off);

											lines.push(model.vertices[tri + vert] + faceNormals[tri / 9][0] * off,
											  model.vertices[tri + vert + 1] + faceNormals[tri / 9][1] * off,
											  model.vertices[tri + vert + 2] + faceNormals[tri / 9][2] * off);

											lines.push(model.vertices[tri + vert] + faceNormals[triOther / 9][0] * off,
											  model.vertices[tri + vert + 1] + faceNormals[triOther / 9][1] * off,
											  model.vertices[tri + vert + 2] + faceNormals[triOther / 9][2] * off);


											touching = true;
										}

									}
									break otherVert;
								}
							}
						}
						if (touching)
						{
							adjTriangles++;
							if (adjTriangles == 3)
							{
								break;
							}
						}
					}

				}
				model["vertices"].push.apply(model.vertices, lines);
				model["normals"].push.apply(model.normals, new Array(lines.length).fill(0));
				model["uvs"].push.apply(model.uvs, new Array(lines.length).fill(-1));
			}
		}
					 */
					#endregion


					//Speed this up!!
					var toLookup = new List<KeyValuePair<string, int>>();
					for (int j = faces.Count - 1; j >= 0; j--)
					{
						if (faces[j].Length > 2)
						{
							for (var vert = 0; vert < 3; vert++)
							{
								toLookup.Add(new KeyValuePair<string, int>(vertices[faces[j][vert * length]], j));
							}
						}
					}
					//TODO [][] is faster, not anymore??
					double[,] bary = new double[faces.Count, 3]; //Filled with: default( int )
					const double threshold = 0.6;
					const double addTo = 1 - threshold;
					int prevJ = faces.Count;
					Lookup<string, int> adj = (Lookup<string, int>)toLookup.ToLookup((item) => item.Key, (item) => item.Value);
					/*
					 Lookup<string,int> blabla;
					 string = vertices[faces[j][vert * length]] (So, the point's XYZ coordinates!!)
					 int = j (The triangle's index)
					 */
					//Each face/triangle has 3 bary coords
					//Lines:
					//TODO finish this
					for (int j = faces.Count - 1; j >= 0; j--)
					{
						if (faces[j].Length > 2)
						{
							IEnumerable<int> matchingVertices0 = adj[vertices[faces[j][0]]];
							IEnumerable<int> matchingVertices1 = adj[vertices[faces[j][length]]];
							IEnumerable<int> matchingVertices2 = adj[vertices[faces[j][2 * length]]];
							//2 Matching points
							//TriIndex = triangle index of the adjacent triangle
							foreach (int triIndex in matchingVertices1)
							{
								if (matchingVertices0.Contains(triIndex))
								{
									double angleBetweenTriangles = Math.Abs(dotProcuct(faceNormals[j], faceNormals[triIndex]));
									if (angleBetweenTriangles < threshold)
									{
										bary[j, 2] = angleBetweenTriangles + addTo;
										//TODO Make this better? (Other triangle bary coords)
										//bary[triIndex, ] = angleBetweenTriangles + addTo;
									}
								}
							}
							foreach (int triIndex in matchingVertices2)
							{
								if (matchingVertices1.Contains(triIndex))
								{
									double angleBetweenTriangles = Math.Abs(dotProcuct(faceNormals[j], faceNormals[triIndex]));
									if (angleBetweenTriangles < threshold)
									{
										bary[j, 0] = angleBetweenTriangles + addTo;
									}
								}
								if (matchingVertices0.Contains(triIndex))
								{
									double angleBetweenTriangles = Math.Abs(dotProcuct(faceNormals[j], faceNormals[triIndex]));
									if (angleBetweenTriangles < threshold)
									{
										bary[j, 1] = angleBetweenTriangles + addTo;
									}
								}
							}
						}
						//Single matching points:						
						if (prevJ - j > 1000)
						{
							Console.WriteLine("{0}% done", (1 - (double)j / faces.Count) * 100);
							prevJ = j;
						}
					}



					//Write to file
					int boneInd = 0;
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
						else if (currFace.Length == 2)
						{
							//Change the bone index
							boneInd = currFace[0];
							//TODO
							//http://stackoverflow.com/questions/16572362/rearrange-a-list-based-on-given-order-in-c-sharp
							//parts[currFace[0]]
						}
						else
						{
							string[] baryCoordsOfTri = toBary(bary[j, 0], bary[j, 1], bary[j, 2]);
							//Triangle
							for (int i = 0; i < 3; i++)
							{
								JSONFile.Write(vertices[currFace[i * 3]]);


								if (length == 2)
								{
									JSONFile.Write("," + uvs[currFace[i * 3 + 1]]);
								}
								else if (length == 3)
								{
									if (hasUvs)
									{
										JSONFile.Write("," + uvs[currFace[i * 3 + 1]]);
									}
									JSONFile.Write("," + normals[currFace[i * 3 + 2]]);
								}
								//TODO Find adjacent faces using indices and a for loop and change this:
								//TODO Fix this
								//If it is a !0 value, the line has to be drawn, otherwise hidden
								//IF (bary[j, i] == 0){hideLine();}
								//if (bary[j, i] == 0)
								//{
								//	printError("Null");
								//	JSONFile.Write(baryString[bary[j, i]]);
								//baryString[i+1];
								//}
								//else
								//{
								//	JSONFile.Write(baryString[i + 1]);
								//}
								//JSONFile.Write(baryString[bary[j, i]]);
								JSONFile.Write(baryCoordsOfTri[i]);
								JSONFile.Write("," + boneInd);
								//Pawsome JS is perfectly capable of realizing that the trailing commas aren't part of the array!
								JSONFile.Write(',');
							}
						}
					}


					JSONFile.Write("]];");
					//You are going to have to reorder the parts manually
					JSONFile.Write("\nvar bones = [");
					for (int i = 0; i < parts.Count; i++)
					{
						//pitch/yaw or something else..?
						JSONFile.Write("{name:" + parts[i] + ",index:" + i + ",pos:[0,0,0],pitch:0,yaw:0},");
					}

					JSONFile.Write("];");
					//TODO add info about how the model is structured
					//TODO create indices file to make finding adjacent stuff faster
					Console.WriteLine("Vertex count: " + vertices.Count);
					Console.WriteLine("UVs count: " + uvs.Count);
					Console.WriteLine("Normals count: " + normals.Count);
					Console.WriteLine("Face count: " + faces.Count);
					Console.WriteLine("Length: " + length);

					JSONFile.Close();

					try { File.Delete("./obj/output.js"); } catch (Exception e) { };
					File.Move("./obj/output.txt", Path.ChangeExtension("./obj/output.txt", ".js"));
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
			var length = Math.Sqrt(x * x + y * y + z * z);
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

		//TODO needs to be changed 
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
}


