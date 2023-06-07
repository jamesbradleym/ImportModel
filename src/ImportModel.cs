using Rhino.FileIO;
using Rhino.Geometry;
using System.Collections.Generic;
using Elements;
using Elements.Geometry;
using System.IO;

namespace ImportModel
{
    public static class ImportModel
    {
        /// <summary>
        /// The ImportModel function.
        /// </summary>
        /// <param name="model">The input model.</param>
        /// <param name="input">The arguments to the execution.</param>
        /// <returns>A ImportModelOutputs instance containing computed results and the model with any new elements.</returns>
        public static ImportModelOutputs Execute(Dictionary<string, Model> inputModels, ImportModelInputs input)
        {
            var model = new Model();
            var warnings = new List<string>();

            // Read and convert supported file types
            foreach (var inputFile in input.Models.Where(obj => obj.File != null).Select(obj => obj.File).ToList())
            {
                // var inputFile = input.Model;
                var filePath = inputFile.LocalFilePath;

                if (!File.Exists(filePath))
                {
                    // string folderPath = Path.Combine(AppContext.BaseDirectory, "Models");
                    string folderPath = "/Users/jamesbradleym/Dropbox (Personal)/Programming/Hypar/ImportModel/Models";
                    filePath = Path.Combine(folderPath, Path.GetFileName(inputFile.Key));

                    if (!File.Exists(filePath))
                    {
                        return null;
                    }
                }

                var fileExtension = Path.GetExtension(filePath);
                if (File.Exists(filePath))
                {
                    switch (fileExtension.ToLower())
                    {
                        case ".3dm":
                            var models = ReadRhinoFile(filePath);
                            model.AddElements(models);
                            break;

                        case ".obj":
                            var objModels = ReadObjFile(filePath, input.ShowEdges);
                            model.AddElements(objModels);
                            break;

                        case ".fbx":
                            var fbxModels = ReadFbxFile(filePath);
                            model.AddElements(fbxModels);
                            break;

                        case ".json":
                            var jsonModels = ReadJSONFile(filePath);
                            model.AddElements(jsonModels);
                            break;

                        // Add additional cases for other supported file types

                        default:
                            // Unsupported file type
                            break;
                    }
                }
                else
                {
                    warnings.Add($"File does not exist: {filePath}");
                }
            }

            var output = new ImportModelOutputs();
            output.Model = model;
            TransformOverrides(input.Overrides, output.Model);
            if (input.ShowEdges)
            {
                output.Model.AddElements(ExtractUniqueEdges(output.Model));
            }
            output.Warnings.AddRange(warnings);
            return output;
        }

        private static string[] ReadAllLinesFromFile(string filePath)
        {
            string[] flines;
            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            {
                flines = File.ReadAllLines(filePath);
            }

            if (flines.Length == 0)
            {
                return null;
            }

            return flines;
        }
        private static Element ReadObjFile(string filePath, bool showEdges)
        {
            var flines = ReadAllLinesFromFile(filePath);

            if (flines == null)
            {
                return null;
            }

            var vertices = new List<Vertex>();
            var vertexNormals = new List<Vector3>();
            var textureCoordinates = new List<UV>();
            var faceIndices = new List<int>();
            var triangles = new List<Triangle>();

            foreach (var line in flines)
            {
                var elements = line.Split(' ');

                if (elements[0] == "v")
                {
                    // Vertex position
                    var x = double.Parse(elements[1]);
                    var y = double.Parse(elements[2]);
                    var z = double.Parse(elements[3]);
                    var position = new Vector3(x, y, z);
                    vertices.Add(new Vertex(position));
                }
                else if (elements[0] == "vn")
                {
                    // Vertex normal
                    var nx = double.Parse(elements[1]);
                    var ny = double.Parse(elements[2]);
                    var nz = double.Parse(elements[3]);
                    var normal = new Vector3(nx, ny, nz);
                    vertexNormals.Add(normal);
                }
                else if (elements[0] == "vt")
                {
                    // Texture coordinates
                    var u = double.Parse(elements[1]);
                    var v = double.Parse(elements[2]);
                    var uv = new UV(u, v);
                    textureCoordinates.Add(uv);
                }
                else if (elements[0] == "f")
                {
                    // Face indices
                    for (var i = 1; i < elements.Length; i++)
                    {
                        var vertexData = elements[i].Split('/');
                        var vertexIndex = int.Parse(vertexData[0]) - 1; // Obj indices are 1-based, so we subtract 1
                        var textureIndex = int.Parse(vertexData[1]) - 1; // Obj indices are 1-based, so we subtract 1
                        var normalIndex = int.Parse(vertexData[2]) - 1; // Obj indices are 1-based, so we subtract 1
                        var vertex = vertices[vertexIndex];
                        var normal = vertexNormals[normalIndex];
                        var uv = textureCoordinates[textureIndex];

                        // Assign normal and texture coordinate to vertex
                        vertex.Normal = normal;
                        vertex.UV = uv;

                        faceIndices.Add(vertexIndex);
                    }

                    // Triangulate faces if they have more than 3 vertices
                    if (faceIndices.Count >= 3)
                    {
                        var v1 = vertices[faceIndices[0]];
                        var v2 = vertices[faceIndices[1]];
                        var v3 = vertices[faceIndices[2]];

                        // Create triangles for the face
                        for (var i = 0; i < faceIndices.Count - 2; i++)
                        {
                            var triangle = new Triangle(v1, v2, v3);
                            triangles.Add(triangle);

                            // Move to the next vertices
                            v2 = v3;

                            if (i + 3 < faceIndices.Count)
                            {
                                v3 = vertices[faceIndices[i + 3]];
                            }
                        }
                    }

                    faceIndices.Clear();
                }
            }

            var materialName = Path.GetFileNameWithoutExtension(filePath) + "_MAT";
            var materialColor = new Color(0.952941176, 0.360784314, 0.419607843, 1.0); // F15C6B with alpha 1
            var material = new Material(materialName);
            material.Color = materialColor;
            material.Unlit = true;

            var mesh = new Elements.Geometry.Mesh(vertices, triangles);
            var meshElement = new MeshElement(mesh);
            meshElement.Material = material;
            meshElement.Name = Path.GetFileNameWithoutExtension(filePath);

            return meshElement;
        }
        private static List<Element> ReadJSONFile(string filePath)
        {
            var flines = ReadAllLinesFromFile(filePath);

            if (flines == null)
            {
                return null;
            }

            // Implement the logic to read and convert .json files
            var models = Model.FromJson(File.ReadAllText(filePath)).Elements.Values.ToList();

            return models;
        }
        private static List<Element> ReadFbxFile(string filePath)
        {
            var flines = ReadAllLinesFromFile(filePath);

            if (flines == null)
            {
                return null;
            }

            // Implement the logic to read and convert .fbx files
            var models = new List<Element>();

            // ...

            return models;
        }
        private static List<Element> ReadRhinoFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var doc = File3dm.Read(filePath);

            var models = new List<Element>();

            foreach (var obj in doc.Objects)
            {
                var rhinoGeometry = obj.Geometry;
                var model = RhinoToElements(rhinoGeometry);

                if (model != null)
                {
                    models.Add(model);
                }
            }

            return models;
        }
        private static Element RhinoToElements(GeometryBase rhinoGeometry)
        {
            if (rhinoGeometry is Rhino.Geometry.Curve rhinoCurve)
            {
                // Convert Rhino curve to Elements curve
                var startPoint = rhinoCurve.PointAtStart;
                var endPoint = rhinoCurve.PointAtEnd;

                var start = new Vector3(startPoint.X, startPoint.Y, startPoint.Z);
                var end = new Vector3(endPoint.X, endPoint.Y, endPoint.Z);

                return new Elements.Geometry.Line(start, end);
            }
            else if (rhinoGeometry is Rhino.Geometry.Mesh rhinoMesh)
            {
                var vertices = new List<Vertex>();
                var triangles = new List<Triangle>();

                foreach (var vertex in rhinoMesh.Vertices)
                {
                    vertices.Add(new Vertex(new Vector3(vertex.X, vertex.Y, vertex.Z)));
                }

                foreach (var face in rhinoMesh.Faces)
                {
                    if (face.IsQuad)
                    {
                        var triangleA = new Triangle(vertices[face.A], vertices[face.B], vertices[face.C]);
                        var triangleB = new Triangle(vertices[face.C], vertices[face.D], vertices[face.A]);

                        triangles.Add(triangleA);
                        triangles.Add(triangleB);
                    }
                    else
                    {
                        var triangle = new Triangle(vertices[face.A], vertices[face.B], vertices[face.C]);
                        triangles.Add(triangle);
                    }
                }

                var mesh = new Elements.Geometry.Mesh(vertices, triangles);

                var materialName = Path.GetFileNameWithoutExtension("Example");
                var materialColor = new Color(0.952941176, 0.360784314, 0.419607843, 1.0); // F15C6B with alpha 1
                var material = new Material(materialName);
                material.Color = materialColor;
                material.Unlit = true;

                var meshElement = new MeshElement(mesh);
                meshElement.Material = material;

                return meshElement;
            }
            else if (rhinoGeometry is Rhino.Geometry.Brep rhinoBrep)
            {

            }

            // Handle other types of geometry if needed
            // ...

            return null; // Unsupported geometry type
        }
        private static List<Triangle> TriangulatePolygon(List<Vertex> vertices, List<int> faceIndices)
        {
            var triangles = new List<Triangle>();

            // Apply ear clipping algorithm to triangulate the polygon
            var remainingVertices = faceIndices.Select(i => vertices[i]).ToList();

            while (remainingVertices.Count >= 3)
            {
                // Find an ear vertex
                var earVertexIndex = FindEarVertexIndex(remainingVertices);

                if (earVertexIndex != -1)
                {
                    // Create triangle from the ear vertex and its adjacent vertices
                    var v1 = remainingVertices[earVertexIndex];
                    var v2 = remainingVertices[(earVertexIndex + 1) % remainingVertices.Count];
                    var v3 = remainingVertices[(earVertexIndex + 2) % remainingVertices.Count];

                    var triangle = new Triangle(v1, v2, v3);
                    triangles.Add(triangle);

                    // Remove the ear vertex from the remaining vertices
                    remainingVertices.RemoveAt((earVertexIndex + 1) % remainingVertices.Count);
                }
                else
                {
                    // No ear vertex found, break the loop
                    break;
                }
            }

            return triangles;
        }
        private static int FindEarVertexIndex(List<Vertex> vertices)
        {
            var vertexCount = vertices.Count;

            for (var i = 0; i < vertexCount; i++)
            {
                var v0 = vertices[(i - 1 + vertexCount) % vertexCount];
                var v1 = vertices[i];
                var v2 = vertices[(i + 1) % vertexCount];

                if (IsEarVertex(v0, v1, v2, vertices))
                {
                    return i;
                }
            }

            return -1;
        }
        private static bool IsEarVertex(Vertex v0, Vertex v1, Vertex v2, List<Vertex> vertices)
        {
            var trianglePoly = new Polygon(v0.Position, v1.Position, v2.Position);

            foreach (var vertex in vertices)
            {
                if (vertex == v0 || vertex == v1 || vertex == v2)
                {
                    continue;
                }

                if (trianglePoly.Covers(vertex.Position))
                {
                    return false;
                }
            }

            return true;
        }
        private static Elements.Geometry.Mesh CreateMeshFromVerticesAndFaces(List<Vertex> vertices, List<Triangle> triangles)
        {
            var mesh = new Elements.Geometry.Mesh();

            foreach (var vertex in vertices)
            {
                mesh.AddVertex(vertex.Position, vertex.UV, vertex.Normal);
            }

            foreach (var triangle in triangles)
            {
                var vertexIndices = new List<int>();

                for (var i = 0; i < 3; i++)
                {
                    var vertexIndex = vertices.IndexOf(triangle.Vertices[i]);
                    vertexIndices.Add(vertexIndex);
                }

                var vertexA = vertices[vertexIndices[0]];
                var vertexB = vertices[vertexIndices[1]];
                var vertexC = vertices[vertexIndices[2]];

                mesh.AddTriangle(vertexA, vertexB, vertexC);
            }

            return mesh;
        }
        private static List<ModelCurve> ExtractUniqueEdges(Model model)
        {
            var uniqueEdges = new HashSet<ModelCurve>();

            foreach (var element in model.Elements)
            {
                if (element.Value is MeshElement meshElement)
                {
                    var transform = meshElement.Transform;
                    foreach (var triangle in meshElement.Mesh.Triangles)
                    {
                        var edge1 = new ModelCurve(new Elements.Geometry.Line(triangle.Vertices[0].Position, triangle.Vertices[1].Position));
                        var edge2 = new ModelCurve(new Elements.Geometry.Line(triangle.Vertices[1].Position, triangle.Vertices[2].Position));
                        var edge3 = new ModelCurve(new Elements.Geometry.Line(triangle.Vertices[2].Position, triangle.Vertices[0].Position));

                        edge1.Transform = transform;
                        edge2.Transform = transform;
                        edge3.Transform = transform;

                        uniqueEdges.Add(edge1);
                        uniqueEdges.Add(edge2);
                        uniqueEdges.Add(edge3);
                    }
                }

                // Add additional checks for other element types if needed
            }

            return uniqueEdges.ToList();
        }

        public static void TransformOverrides(dynamic overrides, Model model)
        {
            // Retrieves all instances of elements in the model and converts them into a list.
            var allMeshElements = model.AllElementsOfType<MeshElement>().ToList();
            // Checks if there are any element instances in the model.
            if (!allMeshElements.Any())
            {
                return;
            }

            foreach (var e in allMeshElements)
            {
                // Adds the original location of the element instance to its AdditionalProperties dictionary.
                e.AdditionalProperties["OriginalLocation"] = e.Transform.Origin;
                e.AdditionalProperties["Name"] = e.Name;
            }
            // Checks if overrides and Transforms within the overrides exist.
            if (overrides != null && overrides.Transforms != null)
            {
                // Initializes a new dictionary to store transform overrides.
                var transformOverridesWithNames = new Dictionary<string, dynamic>();
                var transformOverridesWithoutNames = new Dictionary<Vector3, dynamic>();

                foreach (var transformOverride in overrides.Transforms)
                {
                    // Adds the position override to the dictionary, using its original location as the key.
                    // transformOverrides[transformOverride.Identity.OriginalLocation] = transformOverride;
                    if (transformOverride.Identity.Name != null)
                    {
                        transformOverridesWithNames[transformOverride.Identity.Name + transformOverride.Identity.OriginalLocation.ToString()] = transformOverride;
                    }
                    else
                    {
                        transformOverridesWithoutNames[transformOverride.Identity.OriginalLocation] = transformOverride;
                    }
                }

                foreach (var e in allMeshElements)
                {
                    // Checks if the element instance's original location and name exists in the transform overrides.
                    // If it does not exist, the function continues to the next element instance.
                    transformOverridesWithNames.TryGetValue(e.Name + e.Transform.Origin.ToString(), out var transformOverride);

                    if (transformOverride == null)
                    {
                        if (!transformOverridesWithoutNames.TryGetValue(e.Transform.Origin, out transformOverride))
                        {
                            continue;
                        }
                        transformOverride.Identity.Name = e.Name;
                    }
                    try
                    {
                        // Updates the element instance's transform matrix with the override's transform matrix.
                        e.Transform.Matrix = transformOverride.Value.Transform.Matrix;
                        // Adds the override identity to the element instance.
                        Identity.AddOverrideIdentity(e, transformOverride);
                    }
                    catch
                    {
                        Console.WriteLine("Failed to apply an override.");
                    }
                }
            }
        }
    }
}
