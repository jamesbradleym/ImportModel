using Elements.Geometry;
using Elements.Geometry.Solids;
using System;
using System.Collections.Generic;

namespace Elements
{
    public class Jelly : GeometricElement
    {
        private Mesh _baseMesh;
        public Mesh BaseMesh
        {
            get { return _baseMesh; }
            set
            {
                _baseMesh = value;
                UpdateBoundsAndComputeSolid();
                Volume = value.Volume();
                BoundingBox = value.BoundingBox;
                CenterTransform();
                UpdateRepresentations();
            }
        }
        public BBox3 BoundingBox { get; set; }
        public List<Line> Edgework { get; set; }
        public Guid Guid { get; set; }

        // Additional properties for geometric and analytical properties
        public double Area { get; set; }
        public double Volume { get; set; }
        public double Perimeter { get; set; }

        public Jelly(string _Name, Mesh _Mesh, Guid? guid = null)
        {
            this.Guid = guid ?? Guid.NewGuid();
            this.BaseMesh = _Mesh;
            this.Name = _Name;
            this.Edgework = ExtractUniqueEdges();
            SetMaterial();
            this.AdditionalProperties["Name"] = this.Name;
        }

        public void SetMaterial()
        {
            var materialName = this.Name + "_MAT";
            var materialColor = new Color(0.952941176, 0.360784314, 0.419607843, 1.0); // F15C6B with alpha 1
            var material = new Material(materialName);
            material.Color = materialColor;
            // material.Unlit = true;
            this.Material = material;
        }

        public override void UpdateRepresentations()
        {
            // Create representations based on the mesh and model curve
            var rep = new Representation();
            var solidRep = new Solid();
            var lineRep = new Line();

            foreach (var face in this.BaseMesh.Triangles)
            {
                solidRep.AddFace(new Polygon(face.Vertices.Select(v => new Vector3(v.Position.X, v.Position.Y, v.Position.Z)).ToList()));
            }

            // foreach (var edge in this.Edgework)
            // {
            //     solidRep.AddEdge(new Geometry.Solids.Vertex(UIdGenerator.GenerateUId(), edge.Start), new Geometry.Solids.Vertex(UIdGenerator.GenerateUId(), edge.End));
            // }

            var consol = new ConstructedSolid(solidRep);

            rep.SolidOperations.Add(
                consol
            );

            this.Representation = rep;
        }

        public void UpdateTransform(Matrix matrix)
        {
            this.Transform = new Transform(matrix);
        }

        private List<Line> ExtractUniqueEdges()
        {
            var uniqueEdges = new HashSet<Line>();

            if (this.BaseMesh is Mesh mesh)
            {
                foreach (var triangle in mesh.Triangles)
                {
                    var edge1 = new Elements.Geometry.Line(triangle.Vertices[0].Position, triangle.Vertices[1].Position);
                    var edge2 = new Elements.Geometry.Line(triangle.Vertices[1].Position, triangle.Vertices[2].Position);
                    var edge3 = new Elements.Geometry.Line(triangle.Vertices[2].Position, triangle.Vertices[0].Position);

                    uniqueEdges.Add(edge1);
                    uniqueEdges.Add(edge2);
                    uniqueEdges.Add(edge3);
                }
            }

            return uniqueEdges.ToList();
        }

        public static Vector3 CalculateCenter(List<Triangle> triangles)
        {
            // Ensure the list is not empty
            if (triangles == null || triangles.Count == 0)
                throw new ArgumentException("The list of triangles is empty.");

            // Create a list to store all the vertices
            List<Geometry.Vertex> vertices = new List<Geometry.Vertex>();

            // Collect all the vertices from the triangles
            foreach (var triangle in triangles)
            {
                vertices.AddRange(triangle.Vertices);
            }

            // Ensure there are vertices in the list
            if (vertices.Count == 0)
                throw new ArgumentException("The triangles do not contain any vertices.");

            // Get the sum of all positions
            Vector3 sum = vertices.Aggregate(Vector3.Origin, (current, vertex) => current + vertex.Position);

            // Calculate the average position (center point)
            Vector3 center = sum / vertices.Count;

            return center;
        }

        public static Vector3 CalculateCenter(List<Geometry.Vertex> vertices)
        {
            // Ensure the list is not empty
            if (vertices == null || vertices.Count == 0)
                throw new ArgumentException("The list of vertices is empty.");

            // Get the sum of all positions
            Vector3 sum = vertices.Aggregate(Vector3.Origin, (current, vertex) => current + vertex.Position);

            // Calculate the average position (center point)
            Vector3 center = sum / vertices.Count;

            return center;
        }

        private void CenterTransform()
        {
            if (_baseMesh != null && BoundingBox != null)
            {
                // Get the current center of the mesh
                var center = CalculateCenter(_baseMesh.Triangles.ToList());

                // Calculate the translation vector to move the mesh to the origin from center
                var translation = -1 * center;

                HashSet<Geometry.Vertex> modifiedVertices = new HashSet<Geometry.Vertex>();
                // Translate the vertices of the mesh to center it at the origin
                foreach (var vertex in _baseMesh.Vertices)
                {
                    if (!modifiedVertices.Contains(vertex))
                    {
                        vertex.Position += translation;
                        modifiedVertices.Add(vertex);
                    }
                }

                this.AdditionalProperties["OriginalLocation"] = center;
                this.AdditionalProperties["Vertices"] = _baseMesh.Vertices;
                this.AdditionalProperties["Triangles"] = _baseMesh.Triangles;
                Transform = new Transform(center);
            }
        }
    }


    public class UIdGenerator
    {
        private static readonly Random random = new Random();
        private static uint counter = 0;

        public static uint GenerateUId()
        {
            uint randomValue = (uint)random.Next();
            uint uniqueId = randomValue ^ counter++;
            return uniqueId;
        }
    }
}
