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
        /// Not stored in pairs of 3/triangles
        /// </summary>
        static List<VertBone> bones = new List<VertBone>();
        /// <summary>
        /// Texture names, if the vertices have only 1 element, a new texture will be used
        /// </summary>
        static List<string> texNames = new List<string>();
        /// <summary>
        /// Face normals, crease threshold: 134.43
        /// </summary>
        static List<Vector3D> faceNormals = new List<Vector3D>();

        static List<KeyValuePair<Vector3D, triAndVertIndex>> toLookup = new List<KeyValuePair<Vector3D, triAndVertIndex>>();
        //Vector3D implements GetHashCode
        static Lookup<Vector3D, triAndVertIndex> adjVert;

        static readonly double THRESHOLD = -Math.Cos(134.43 * Math.PI / 180);
        //Each vertex has a bone

        //TODO Bone offsets and stuff
        static Dictionary<string, BoneInfo> boneNodes = new Dictionary<string, BoneInfo>();

        static Scene scene;

        const int MAX_INFLUENCES = 4;

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
            string fileName = "./obj/" + getUserInput("File name");

            if (fileName == "./obj/")
            {
                string[] possibleFiles = Directory.GetFiles("./obj");
                foreach (string currFileName in possibleFiles)
                {
                    if (!currFileName.EndsWith(".txt") && !currFileName.EndsWith(".js"))
                    {
                        fileName = currFileName;
                        break;
                    }
                }
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

            importer.SetConfig(new SortByPrimitiveTypeConfig(PrimitiveType.Line | PrimitiveType.Point));
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
            scene = importer.ImportFile(fileName, PostProcessPreset.TargetRealTimeMaximumQuality | PostProcessSteps.FlipUVs | PostProcessSteps.OptimizeMeshes | PostProcessSteps.SortByPrimitiveType);
            parseNode(scene.RootNode);
            //TODO Get all the relevant nodes and create a bone tree!
            createBoneTree(scene.RootNode, -1, Matrix4x4.Identity);
            //End of example
            importer.Dispose();
            adjVert = (Lookup<Vector3D, triAndVertIndex>)toLookup.ToLookup((item) => item.Key, (item) => item.Value);

            //First 3 => Point, Second 3 => Line
            //TODO Make this a bit better
            //For each triangle, store some bary coords
            double[,] bary = new double[normals.Count, 6]; //Filled with: default( int )
                                                           //Edit

            #region Bary coords and bones
            //Lines:
            for (int j = 0; j < toLookup.Count; j += 3)
            {
                Vector3D v0 = toLookup[j + 2].Key - toLookup[j + 1].Key;
                Vector3D v1 = toLookup[j + 2].Key - toLookup[j].Key;
                Vector3D v2 = toLookup[j + 1].Key - toLookup[j].Key;
                double area = Math.Abs(Vector3D.Cross(v1, v2).Length()) / 2; //Determinant of a 2D matrix, used to calculate the area of a parallelogram


                IEnumerable<triAndVertIndex> matchingVertices0 = adjVert[toLookup[j].Key];
                IEnumerable<triAndVertIndex> matchingVertices1 = adjVert[toLookup[j + 1].Key];
                IEnumerable<triAndVertIndex> matchingVertices2 = adjVert[toLookup[j + 2].Key];
                //2 Matching points
                //TriIndex = triangle index of the adjacent triangle
                foreach (triAndVertIndex index in matchingVertices0)
                {
                    //Oh, yeah! It's working! (Magic!)
                    //TODO turn this into a function as well
                    foreach (triAndVertIndex otherIndex in matchingVertices1)
                    {
                        //If it is part of the same line
                        if (otherIndex.triIndex == index.triIndex)
                        {
                            double angleBetweenTriangles = (Vector3D.Dot(faceNormals[j / 3], faceNormals[otherIndex.triIndex]));
                            if (angleBetweenTriangles < THRESHOLD)
                            {

                                //area = 1/2*base*height
                                //2*area / base = height 
                                /*
								dist = vec3(area / v0.Length(), 0, 0);
								gl_Position = gl_PositionIn[0];
								EmitVertex();
								dist = vec3(0, area / v1.Length(), 0);
								gl_Position = gl_PositionIn[1];
								EmitVertex();
								dist = vec3(0, 0, area / v2.Length());
								gl_Position = gl_PositionIn[2];
								EmitVertex();*/
                                bary[j / 3, 5] = area / v2.Length(); // 1;// angleBetweenTriangles + addTo;
                            }
                            //If we found the adjacent triangle, we can go to the next one
                            break;
                        }
                    }
                }
                foreach (triAndVertIndex index in matchingVertices1)
                {
                    foreach (triAndVertIndex otherIndex in matchingVertices2)
                    {
                        if (otherIndex.triIndex == index.triIndex)
                        {
                            double angleBetweenTriangles = (Vector3D.Dot(faceNormals[j / 3], faceNormals[otherIndex.triIndex]));
                            if (angleBetweenTriangles < THRESHOLD)
                            {
                                bary[j / 3, 3] = area / v0.Length();// TODO angleBetweenTriangles + addTo;
                            }
                            break;
                        }
                    }
                }
                foreach (triAndVertIndex index in matchingVertices2)
                {
                    foreach (triAndVertIndex otherIndex in matchingVertices0)
                    {
                        if (otherIndex.triIndex == index.triIndex)
                        {
                            double angleBetweenTriangles = (Vector3D.Dot(faceNormals[j / 3], faceNormals[otherIndex.triIndex]));
                            if (angleBetweenTriangles < THRESHOLD)
                            {
                                bary[j / 3, 4] = area / v1.Length();// TODO angleBetweenTriangles + addTo;
                            }
                            break;
                        }
                    }
                }
            }

            //Merge lines
            for (int j = 0; j < toLookup.Count; j += 3)
            {
                Vector3D v0 = toLookup[j + 2].Key - toLookup[j + 1].Key;
                Vector3D v1 = toLookup[j + 2].Key - toLookup[j].Key;
                Vector3D v2 = toLookup[j + 1].Key - toLookup[j].Key;
                double area = Math.Abs(Vector3D.Cross(v1, v2).Length()) / 2; //Determinant of a 2D matrix, used to calculate the area of a parallelogram

                IEnumerable<triAndVertIndex> matchingVertices0 = adjVert[toLookup[j].Key];
                IEnumerable<triAndVertIndex> matchingVertices1 = adjVert[toLookup[j + 1].Key];
                IEnumerable<triAndVertIndex> matchingVertices2 = adjVert[toLookup[j + 2].Key];

                int numberOfAdjBary = 0;

                //Index of the adjacent triangle
                foreach (triAndVertIndex index in matchingVertices0)
                {
                    //TODO turn this into a function as well
                    if ((bary[index.triIndex, ((index.vertIndex + 1) % 3) + 3] > 0 || bary[index.triIndex, ((index.vertIndex + 2) % 3) + 3] > 0)
                        && index.triIndex != j / 3)
                    {
                        numberOfAdjBary++;
                    }
                }
                //Every line is actually 2 lines
                if (numberOfAdjBary >= 4)
                {
                    bary[j / 3, 0] = area / v0.Length();
                }
                numberOfAdjBary = 0;
                foreach (triAndVertIndex index in matchingVertices1)
                {
                    if ((bary[index.triIndex, ((index.vertIndex + 1) % 3) + 3] > 0 || bary[index.triIndex, ((index.vertIndex + 2) % 3) + 3] > 0)
                        && index.triIndex != j / 3)
                    {
                        numberOfAdjBary++;
                    }
                }
                if (numberOfAdjBary >= 4)
                {
                    bary[j / 3, 1] = area / v1.Length();
                }
                numberOfAdjBary = 0;
                foreach (triAndVertIndex index in matchingVertices2)
                {
                    if ((bary[index.triIndex, ((index.vertIndex + 1) % 3) + 3] > 0 || bary[index.triIndex, ((index.vertIndex + 2) % 3) + 3] > 0)
                        && index.triIndex != j / 3)
                    {
                        numberOfAdjBary++;
                    }
                }
                if (numberOfAdjBary >= 4)
                {
                    bary[j / 3, 2] = area / v2.Length();
                }
            }
            #endregion


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
                    JSONFile.Write('"' + texNames[(int)currVert[0].X] + '"');
                    texCount++;
                }
                else
                {
                    //Edit
                    string[] baryCoordsOfTri = toBary(bary, j - texCount);
                    //Triangle
                    for (int i = 0; i < 3; i++)
                    {
                        JSONFile.Write(Vec3DToString(currVert[i]));
                        JSONFile.Write(UVToString(uvs[j - texCount][i]));
                        JSONFile.Write(Vec3DToString(normals[j - texCount][i]));

                        JSONFile.Write(baryCoordsOfTri[i]);
                        //TODO Output bone IDs
                        JSONFile.Write("," + bones[j - texCount + i].BoneIDs[0]/*.Weights[0]*/);
                    }
                }
            }


            JSONFile.Write("]];");
            JSONFile.Close();


            StreamWriter bonesFile = File.CreateText("./obj/outputBones.txt");
            //You are going to have to reorder the parts manually
            bonesFile.Write("\nbones = [");

            foreach (KeyValuePair<string, BoneInfo> boneNode in boneNodes)
            {
                //TODO Output bones, already ordered correctly!
                //pitch/yaw or something else..?
                // ",pos:[0,0,0],pitch:0,yaw:0},"
                //TODO Number of bones
                bonesFile.WriteLine("{name:\"" + boneNode.Key + "\",index:" + boneNode.Value.BoneID + ",parent:" + boneNode.Value.Parent + ",pos:[" + "],rot:[" + "]},");
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

        static void createBoneTree(Node currentNode, int parentBoneID, Matrix4x4 accumulatedMatrix)
        {
            accumulatedMatrix = accumulatedMatrix * currentNode.Transform;
            if (boneNodes.ContainsKey(currentNode.Name))
            {
                boneNodes[currentNode.Name].AddNodeMatrix(accumulatedMatrix);
                boneNodes[currentNode.Name].Parent = parentBoneID;
                accumulatedMatrix = Matrix4x4.Identity;
                parentBoneID = boneNodes[currentNode.Name].BoneID;
                //TODO
            }

            for (int i = 0; i < currentNode.ChildCount; i++)
            {
                createBoneTree(currentNode.Children[i], parentBoneID, accumulatedMatrix);
            }
        }

        static int numberOfBones = 0;
        static string prevMaterial = "";
        static void parseNode(Node currentNode)
        {
            if (currentNode.HasMeshes)
            {
                //Console.Write(currentNode.Name);
                foreach (int index in currentNode.MeshIndices)
                {
                    Mesh mesh = scene.Meshes[index];

                    //TODO bones
                    //TODO mesh doesn't have bones attached case
                    //mesh.MaterialIndex
                    //scene.Materials
                    //Console.WriteLine("Material: " + scene.Materials[mesh.MaterialIndex].Name);

                    #region Bones
                    VertBone[] currBones = new VertBone[mesh.FaceCount * 3];
                    foreach (Bone currBone in mesh.Bones)
                    {
                        if (boneNodes.ContainsKey("currBone.Name"))
                        {
                            Debugger.Break();
                            //TODO Implement the case where a bone node is referenced multiple times
                        }
                        else
                        {
                            boneNodes[currBone.Name] = new BoneInfo { BoneOffsetMatrix = currBone.OffsetMatrix, BoneID = numberOfBones };
                            Debug.Assert(currBone.Name != "" && currBone.Name != null, "Error, the bone name is invalid: " + currBone.Name);
                            foreach (VertexWeight vertBone in currBone.VertexWeights)
                            {
                                //Temp array, will get converted a bit later on
                                if (currBones[vertBone.VertexID] == null)
                                {
                                    currBones[vertBone.VertexID] = new VertBone();
                                }
                                //TODO Get the ID of the current bone, different mesh.Bones can refer to the same (Bone)Node
                                currBones[vertBone.VertexID].AddBone(vertBone.Weight, numberOfBones);
                                //vertBone.VertexID
                            }
                            numberOfBones++;

                        }
                    }

                    #endregion

                    #region Texture
                    if (mesh.TextureCoordinateChannelCount <= 0)
                    {
                        PrintCrucialError("No textures");
                    }

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
                            texFilePath = "nonexistent.png";
                        }

                        string color = ",1,1,1";
                        if (scene.Materials[mesh.MaterialIndex].HasColorAmbient)
                        {
                            color = ";" + scene.Materials[mesh.MaterialIndex].ColorAmbient.R + ";" + scene.Materials[mesh.MaterialIndex].ColorAmbient.G + ";" + scene.Materials[mesh.MaterialIndex].ColorAmbient.B;
                        }
                        else if (scene.Materials[mesh.MaterialIndex].HasColorDiffuse)
                        {
                            color = ";" + scene.Materials[mesh.MaterialIndex].ColorDiffuse.R + ";" + scene.Materials[mesh.MaterialIndex].ColorDiffuse.G + ";" + scene.Materials[mesh.MaterialIndex].ColorDiffuse.B;
                        }
                        texNames.Add(texFilePath + color);
                    }
                    #endregion

                    if (!mesh.HasNormals)
                    {
                        PrintCrucialError("No normals");
                    }

                    //Console.WriteLine(mesh.Vertices.Count + ":" + mesh.Normals.Count + ":" + mesh.VertexCount + ":" + mesh.TextureCoordinateChannels[0].Count);
                    foreach (Face face in mesh.Faces)
                    {
                        Debug.Assert(face.IndexCount == 3, "Not triangulated face");

                        int indexX = face.Indices[0], indexY = face.Indices[1], indexZ = face.Indices[2];

                        Vector3D[] points = new Vector3D[] { mesh.Vertices[indexX], mesh.Vertices[indexY], mesh.Vertices[indexZ] };
                        vertices.Add(points);
                        //Vector3Ds with z = 0
                        //Only capable of handling one texture
                        uvs.Add(new Vector3D[] { mesh.TextureCoordinateChannels[0][indexX], mesh.TextureCoordinateChannels[0][indexY], mesh.TextureCoordinateChannels[0][indexZ] });
                        normals.Add(new Vector3D[] { mesh.Normals[indexX], mesh.Normals[indexY], mesh.Normals[indexZ] });
                        //Conversion, vertex IDs are taken care of
                        bones.Add(currBones[indexX]);
                        bones.Add(currBones[indexY]);
                        bones.Add(currBones[indexZ]);

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
        static void PrintCrucialError(string errorMessage)
        {
            printError(errorMessage);
            Debugger.Break();
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
        static string[] toBary(double[,] baryArr, int index)
        {
            double[] bary = {
            1,1,1,
            1,1,1,
            1,1,1};
            //Points
            if (baryArr[index, 0] > 0 && baryArr[index, 4] <= 0 && baryArr[index, 5] <= 0)
            {

                bary[1] = 0;
                bary[4] = baryArr[index, 0];
                bary[7] = baryArr[index, 0];

            }

            if (baryArr[index, 1] > 0 && baryArr[index, 3] <= 0 && baryArr[index, 5] <= 0)
            {
                bary[2] = baryArr[index, 1];
                bary[5] = 0;
                bary[8] = baryArr[index, 1];
            }

            if (baryArr[index, 2] > 0 && baryArr[index, 3] <= 0 && baryArr[index, 4] <= 0)
            {
                bary[0] = baryArr[index, 2];
                bary[3] = baryArr[index, 2];
                bary[6] = 0;
            }
            //Lines
            if (baryArr[index, 3] > 0)
            {
                bary[0] = baryArr[index, 3];
                bary[3] = 0;
                bary[6] = 0;
            }

            if (baryArr[index, 4] > 0)
            {
                bary[1] = 0;
                bary[4] = baryArr[index, 4];
                bary[7] = 0;
            }

            if (baryArr[index, 5] > 0)
            {
                bary[2] = 0;
                bary[5] = 0;
                bary[8] = baryArr[index, 5];
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
    }
    struct triAndVertIndex
    {
        public int triIndex;
        public int vertIndex;
    }
    class BoneInfo
    {
        private Matrix4x4 boneOffsetMatrix;
        private int boneID;
        private int parent;

        public Matrix4x4 BoneOffsetMatrix
        {
            get
            {
                return boneOffsetMatrix;
            }

            set
            {
                boneOffsetMatrix = value;
            }
        }

        public int BoneID
        {
            get
            {
                return boneID;
            }

            set
            {
                boneID = value;
            }
        }

        public int Parent
        {
            get
            {
                return parent;
            }

            set
            {
                parent = value;
            }
        }

        public void AddNodeMatrix(Matrix4x4 accumulatedMatrix)
        {
            boneOffsetMatrix = accumulatedMatrix * boneOffsetMatrix;
        }
    }

    class VertBone
    {
        private int[] _boneIDs = new int[4];
        private float[] _weights = new float[4];
        private int _numberOfBones = 0;

        public float[] Weights
        {
            get
            {
                return _weights;
            }

            set
            {
                _weights = value;
            }
        }

        public int NumberOfBones
        {
            get
            {
                return _numberOfBones;
            }

            set
            {
                _numberOfBones = value;
            }
        }

        public int[] BoneIDs
        {
            get
            {
                return _boneIDs;
            }

            set
            {
                _boneIDs = value;
            }
        }

        public void AddBone(float weight, int id)
        {
            _weights[_numberOfBones] = weight;
            BoneIDs[_numberOfBones] = id;
            _numberOfBones++;
        }
    }
}


