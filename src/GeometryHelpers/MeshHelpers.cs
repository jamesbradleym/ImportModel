using Elements.Geometry;
using System.Collections.Generic;

namespace ImportModel
{
    public static class Triangulator
    {
        public static List<Triangle> Triangulate(List<Vertex> vertices)
        {
            List<Triangle> triangles = new List<Triangle>();

            double dist1 = vertices[0].Position.DistanceTo(vertices[2].Position);
            double dist2 = vertices[1].Position.DistanceTo(vertices[3].Position);
            if (dist1 > dist2)
            {
                triangles.Add(new Triangle(vertices[0], vertices[1], vertices[3]));
                triangles.Add(new Triangle(vertices[1], vertices[2], vertices[3]));
            }
            else
            {
                triangles.Add(new Triangle(vertices[0], vertices[1], vertices[3]));
                triangles.Add(new Triangle(vertices[1], vertices[2], vertices[3]));
            }

            return triangles;
        }
    }
}
