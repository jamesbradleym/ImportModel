using Rhino.FileIO;
using Rhino.Geometry;
using System.Collections.Generic;
using Elements;
using Elements.Geometry;
using System.IO;
using System.Reflection;
using MeshIO.FBX;
using MeshIO;
using MeshIO.Entities.Geometries;

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

            // var keyValueString = string.Join(", ", input.SignedResourceUrls.InputData.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            // warnings.Add($"Dictionary: {keyValueString}");
            // warnings.Add("LocalFilePath: " + input.Single.LocalFilePath + ", Key: " + input.Single.Key);
            // Read and convert supported file types
            foreach (var modelInput in input.Models.Where(obj => obj.File != null).ToList())
            {
                var inputFile = modelInput.File;
                var filePath = inputFile.LocalFilePath;

                if (!File.Exists(filePath))
                {
                    if (inputFile.Key == null)
                    {
                        warnings.Add("LocalFilePath: " + inputFile.LocalFilePath + ", Key: " + inputFile.Key + ", Does not exist.");
                        continue;
                    }
                    // string folderPath = Path.Combine(AppContext.BaseDirectory, "Models");
                    string folderPath = "/Users/jamesbradleym/Dropbox (Personal)/Programming/Hypar/ImportModel/Models";
                    filePath = Path.Combine(folderPath, Path.GetFileName(inputFile.Key));

                    if (!File.Exists(filePath))
                    {
                        warnings.Add("LocalFilePath: " + inputFile.LocalFilePath + ", Key: " + inputFile.Key + ", Does not exist.");
                        continue;
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
                            var objModels = ReadObjFile(filePath, modelInput.Disjoint);
                            model.AddElements(objModels);
                            break;

                        case ".fbx":
                            var fbxModels = ReadFbxFile(filePath);
                            model.AddElements(fbxModels);
                            break;

                        case ".json":
                            var jsonModels = ReadJSONFile(filePath, modelInput.Disjoint);
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
            // if (input.ShowEdges)
            // {
            //     output.Model.AddElements(ExtractUniqueEdges(output.Model));
            // }
            output.Warnings.AddRange(warnings);
            return output;
        }

        private static string[]? ReadAllLinesFromFile(string filePath)
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
        private static List<Jelly>? ReadObjFile(string filePath, bool disjoint)
        {
            var jellies = new List<Jelly>();

            var flines = ReadAllLinesFromFile(filePath);

            if (flines == null)
            {
                return null;
            }

            var vertices = new List<Vertex>();
            var vertexNormals = new List<Vector3>();
            var textureCoordinates = new List<UV>();
            var faceIndices = new List<int>();
            var triangles = new List<Elements.Geometry.Triangle>();
            var currentMeshName = string.Empty;
            var identifierSet = new List<string>();
            var shouldConfirm = false;

            foreach (var line in flines)
            {
                var elements = line.Split(' ');

                if (elements.Length == 0)
                {
                    continue;
                }
                else if (elements[0] == "")
                {
                    shouldConfirm = true;
                }

                var identifier = elements[0];

                if (disjoint && shouldConfirm && identifierSet.Contains(identifier))
                {
                    // Group or Object name (new distinct mesh)
                    if (triangles.Count > 0)
                    {
                        List<Vertex> verticesCulled = triangles.SelectMany(t => t.Vertices).Distinct().ToList();
                        var mesh = new Elements.Geometry.Mesh(verticesCulled, triangles);
                        var jelly = new Jelly($"{Path.GetFileNameWithoutExtension(filePath)}-{currentMeshName}", mesh);
                        jellies.Add(jelly);

                        // Clear current mesh data
                        triangles.Clear();
                        currentMeshName = string.Empty;
                        identifierSet.Clear();
                    }
                }
                else
                {
                    shouldConfirm = false;
                }

                if (!identifierSet.Contains(identifier))
                {
                    identifierSet.Add(identifier);
                }

                if (identifier == "v")
                {
                    // Vertex position
                    var x = double.Parse(elements[1]);
                    var y = double.Parse(elements[2]);
                    var z = double.Parse(elements[3]);
                    var position = new Vector3(x, y, z);
                    vertices.Add(new Vertex(position));
                }
                else if (identifier == "vn")
                {
                    // Vertex normal
                    var nx = double.Parse(elements[1]);
                    var ny = double.Parse(elements[2]);
                    var nz = double.Parse(elements[3]);
                    var normal = new Vector3(nx, ny, nz);
                    vertexNormals.Add(normal);
                }
                else if (identifier == "vt")
                {
                    // Texture coordinates
                    var u = double.Parse(elements[1]);
                    var v = double.Parse(elements[2]);
                    var uv = new UV(u, v);
                    textureCoordinates.Add(uv);
                }
                else if (identifier == "f")
                {
                    // Face indices
                    for (var i = 1; i < elements.Length; i++)
                    {
                        var vertexData = elements[i].Split('/');

                        var vertexIndex = int.TryParse(vertexData[0], out var vi) ? vi - 1 : 0;
                        var textureIndex = int.TryParse(vertexData[1], out var ti) ? ti - 1 : 0;
                        var normalIndex = int.TryParse(vertexData[2], out var ni) ? ni - 1 : 0;

                        var vertex = vertices[vertexIndex];

                        var normal = Vector3.Origin;
                        if (vertexNormals.Count > 0)
                        {
                            normal = normalIndex < vertexNormals.Count ? vertexNormals[normalIndex] : Vector3.Origin;
                        }

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
                            var triangle = new Elements.Geometry.Triangle(v1, v2, v3);
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
                else if (identifier == "g" || identifier == "o")
                {
                    currentMeshName = string.Join(" ", elements.Skip(1));
                }
            }

            // Process the last mesh object
            if (triangles.Count > 0)
            {
                var mesh = new Elements.Geometry.Mesh(vertices, triangles);
                var jelly = new Jelly($"{Path.GetFileNameWithoutExtension(filePath)}-{currentMeshName}", mesh);
                jellies.Add(jelly);
            }

            return jellies;
        }

        private static List<Element>? ReadJSONFile(string filePath, bool disjoint)
        {
            var ellies = new List<Element>();
            var flines = ReadAllLinesFromFile(filePath);

            if (flines == null)
            {
                return null;
            }

            // Implement the logic to read and convert .json files
            var models = Model.FromJson(File.ReadAllText(filePath)).Elements.Values.ToList();

            foreach (var elly in models)
            {
                switch (elly)
                {
                    case Jelly jelly:
                        ellies.Add(jelly);
                        break;
                    case MeshElement meshy:
                        var meshjelly = new Jelly($"{Path.GetFileNameWithoutExtension(filePath)}-{meshy.Name}", meshy.Mesh);
                        ellies.Add(meshjelly);
                        break;
                    default:
                        ellies.Add(elly);
                        break;
                }
            }
            return ellies;
        }
        private static List<Jelly>? ReadFbxFile(string filePath)
        {
            var jellies = new List<Jelly>();

            var flines = ReadAllLinesFromFile(filePath);

            if (flines == null)
            {
                return null;
            }

            // Implement the logic to read and convert .fbx files
            using (FbxReader reader = new FbxReader(filePath, ErrorLevel.Checked))
            {
                // reader.OnNotification += NotificationHelper.LogConsoleNotification;
                Scene scene = reader.Read();
                //Iterate throgh all the nodes in the scene
                foreach (Element3D item in scene.RootNode.Nodes)
                {
                    //Check if the element is a geometric type
                    if (item is Geometry geometry)
                    {
                        // jellies.Add(geometry);
                    }

                    if (item is Node node)
                    {
                        // Each node can contain geometric elements, so you need to look in the elements contained in the main node
                        foreach (Element3D c in node.Nodes)
                        {
                            if (c is MeshIO.Entities.Geometries.Mesh m)
                            {
                                List<Vertex> vertices = m.Vertices.Select(xyz => new Vertex(new Vector3(xyz.X, xyz.Y, xyz.Z))).ToList();
                                List<Elements.Geometry.Triangle> triangles = new List<Elements.Geometry.Triangle>();

                                foreach (var p in m.Polygons)
                                {
                                    // Assuming p is a collection of integers representing indices
                                    int[] polygonVertices = p.ToArray();
                                    if (polygonVertices.Length > 3)
                                    {
                                        triangles.AddRange(Triangulator.Triangulate(new List<Vertex>() { vertices[polygonVertices[0]], vertices[polygonVertices[1]], vertices[polygonVertices[2]], vertices[polygonVertices[3]] }));
                                    }
                                    else if (polygonVertices.Length == 3)
                                    {
                                        triangles.Add(new Elements.Geometry.Triangle(vertices[polygonVertices[0]], vertices[polygonVertices[1]], vertices[polygonVertices[2]]));
                                    }
                                }

                                var mesh = new Elements.Geometry.Mesh(vertices, triangles);
                                jellies.Add(new Jelly(item.Name, mesh));
                            }
                        }
                    }

                }
            }

            return jellies;
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
                var triangles = new List<Elements.Geometry.Triangle>();

                foreach (var vertex in rhinoMesh.Vertices)
                {
                    vertices.Add(new Vertex(new Vector3(vertex.X, vertex.Y, vertex.Z)));
                }

                foreach (var face in rhinoMesh.Faces)
                {
                    if (face.IsQuad)
                    {
                        var triangleA = new Elements.Geometry.Triangle(vertices[face.A], vertices[face.B], vertices[face.C]);
                        var triangleB = new Elements.Geometry.Triangle(vertices[face.C], vertices[face.D], vertices[face.A]);

                        triangles.Add(triangleA);
                        triangles.Add(triangleB);
                    }
                    else
                    {
                        var triangle = new Elements.Geometry.Triangle(vertices[face.A], vertices[face.B], vertices[face.C]);
                        triangles.Add(triangle);
                    }
                }

                var mesh = new Elements.Geometry.Mesh(vertices, triangles);

                var materialName = Path.GetFileNameWithoutExtension("Example");
                var materialColor = new Elements.Geometry.Color(0.952941176, 0.360784314, 0.419607843, 1.0); // F15C6B with alpha 1
                var material = new Elements.Material(materialName);
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
        private static Elements.Geometry.Mesh CreateMeshFromVerticesAndFaces(List<Vertex> vertices, List<Elements.Geometry.Triangle> triangles)
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
            var allJellyElements = model.AllElementsOfType<Jelly>().ToList();
            // Checks if there are any element instances in the model.
            if (!allJellyElements.Any())
            {
                return;
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

                foreach (var e in allJellyElements)
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
                        // e.Transform.Matrix = transformOverride.Value.Transform.Matrix;
                        e.UpdateTransform(transformOverride.Value.Transform.Matrix);
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
