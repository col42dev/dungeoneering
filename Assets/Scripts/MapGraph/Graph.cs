using Delaunay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Map
{
    public class Graph
    {
        List<KeyValuePair<int, Corner>> _cornerMap = new List<KeyValuePair<int, Corner>>();

        bool _needsMoreRandomness = false;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public List<Center> centers = new List<Center>();
        public List<Corner> corners = new List<Corner>();
        public List<Edge> edges = new List<Edge>();

        public Graph(IEnumerable<Vector2> points, Voronoi voronoi, int width, int height)
        {
            Width = width;
            Height = height;


            BuildGraph(points, voronoi);
 

            //centers.ForEach(p => p.biome = GetBiome(p));
        }

        private void BuildGraph(IEnumerable<Vector2> points, Delaunay.Voronoi voronoi)
        {
            // Build graph data structure in 'edges', 'centers', 'corners',
            // based on information in the Voronoi results: point.neighbors
            // will be a list of neighboring points of the same type (corner
            // or center); point.edges will be a list of edges that include
            // that point. Each edge connects to four points: the Voronoi edge
            // edge.{v0,v1} and its dual Delaunay triangle edge edge.{d0,d1}.
            // For boundary polygons, the Delaunay edge will have one null
            // point, and the Voronoi edge may be null.
            var libedges = voronoi.Edges();

            var centerLookup = new Dictionary<Vector2?, Center>();

            // Build Center objects for each of the points, and a lookup map
            // to find those Center objects again as we build the graph
            foreach (var point in points)
            {
                var p = new Center { index = centers.Count, point = point };
                centers.Add(p);
                centerLookup[point] = p;
            }

            // Workaround for Voronoi lib bug: we need to call region()
            // before Edges or neighboringSites are available
            foreach (var p in centers)
            {
                voronoi.Region(p.point);
            }

            foreach (var libedge in libedges)
            {
                var dedge = libedge.DelaunayLine();
                var vedge = libedge.VoronoiEdge();

                // Fill the graph data. Make an Edge object corresponding to
                // the edge from the voronoi library.
                var edge = new Edge
                {
                    index = edges.Count,
                    river = 0,

                    // Edges point to corners. Edges point to centers. 
                    v0 = MakeCorner(vedge.p0),
                    v1 = MakeCorner(vedge.p1),
                    d0 = centerLookup[dedge.p0],
                    d1 = centerLookup[dedge.p1]
                };
                if (vedge.p0.HasValue && vedge.p1.HasValue)
                    edge.midpoint = Vector2Extensions.Interpolate(vedge.p0.Value, vedge.p1.Value, 0.5f);

                edges.Add(edge);

                // Centers point to edges. Corners point to edges.
                if (edge.d0 != null) { edge.d0.borders.Add(edge); }
                if (edge.d1 != null) { edge.d1.borders.Add(edge); }
                if (edge.v0 != null) { edge.v0.protrudes.Add(edge); }
                if (edge.v1 != null) { edge.v1.protrudes.Add(edge); }

                // Centers point to centers.
                if (edge.d0 != null && edge.d1 != null)
                {
                    AddToCenterList(edge.d0.neighbors, edge.d1);
                    AddToCenterList(edge.d1.neighbors, edge.d0);
                }

                // Corners point to corners
                if (edge.v0 != null && edge.v1 != null)
                {
                    AddToCornerList(edge.v0.adjacent, edge.v1);
                    AddToCornerList(edge.v1.adjacent, edge.v0);
                }

                // Centers point to corners
                if (edge.d0 != null)
                {
                    AddToCornerList(edge.d0.corners, edge.v0);
                    AddToCornerList(edge.d0.corners, edge.v1);
                }
                if (edge.d1 != null)
                {
                    AddToCornerList(edge.d1.corners, edge.v0);
                    AddToCornerList(edge.d1.corners, edge.v1);
                }

                // Corners point to centers
                if (edge.v0 != null)
                {
                    AddToCenterList(edge.v0.touches, edge.d0);
                    AddToCenterList(edge.v0.touches, edge.d1);
                }
                if (edge.v1 != null)
                {
                    AddToCenterList(edge.v1.touches, edge.d0);
                    AddToCenterList(edge.v1.touches, edge.d1);
                }
            }

            // TODO: use edges to determine these
            var topLeft = centers.OrderBy(p => p.point.x + p.point.y).First();
            AddCorner(topLeft, 0, 0);

            var bottomRight = centers.OrderByDescending(p => p.point.x + p.point.y).First();
            AddCorner(bottomRight, Width, Height);

            var topRight = centers.OrderByDescending(p => Width - p.point.x + p.point.y).First();
            AddCorner(topRight, 0, Height);

            var bottomLeft = centers.OrderByDescending(p => p.point.x + Height - p.point.y).First();
            AddCorner(bottomLeft, Width, 0);

            // required for polygon fill
            foreach (var center in centers)
            {
                center.corners.Sort(ClockwiseComparison(center));
            }
        }

        private static void AddCorner(Center topLeft, int x, int y)
        {
            if (topLeft.point.x != x || topLeft.point.y != y)
                topLeft.corners.Add(new Corner { ocean = true, point = new Vector2(x, y) });
        }

        private Comparison<Corner> ClockwiseComparison(Center center)
        {
            Comparison<Corner> result =
                (a, b) =>
                {
                    return (int)(((a.point.x - center.point.x) * (b.point.y - center.point.y) - (b.point.x - center.point.x) * (a.point.y - center.point.y)) * 1000);
                };
            return result;
        }

        private Corner MakeCorner(Vector2? nullablePoint)
        {
            // The Voronoi library generates multiple Point objects for
            // corners, and we need to canonicalize to one Corner object.
            // To make lookup fast, we keep an array of Points, bucketed by
            // x value, and then we only have to look at other Points in
            // nearby buckets. When we fail to find one, we'll create a new
            // Corner object.

            if (nullablePoint == null)
                return null;

            var point = nullablePoint.Value;

            for (var i = (int)(point.x - 1); i <= (int)(point.x + 1); i++)
            {
                foreach (var kvp in _cornerMap.Where(p => p.Key == i))
                {
                    var dx = point.x - kvp.Value.point.x;
                    var dy = point.y - kvp.Value.point.y;
                    if (dx * dx + dy * dy < 1e-6)
                        return kvp.Value;
                }
            }

            var corner = new Corner { index = corners.Count, point = point };
            corners.Add(corner);
            corner.border = point.x == 0 || point.x == Width || point.y == 0 || point.y == Height;

            _cornerMap.Add(new KeyValuePair<int, Corner>((int)(point.x), corner));

            return corner;
        }

        private void AddToCornerList(List<Corner> v, Corner x)
        {
            if (x != null && v.IndexOf(x) < 0)
                v.Add(x);
        }

        private void AddToCenterList(List<Center> v, Center x)
        {
            if (x != null && v.IndexOf(x) < 0) { v.Add(x); }
        }


        private Edge lookupEdgeFromCenter(Center p, Center r)
        {
            foreach (var edge in p.borders)
            {
                if (edge.d0 == r || edge.d1 == r)
                    return edge;
            }
            return null;
        }

        private Edge lookupEdgeFromCorner(Corner q, Corner s)
        {
            foreach (var edge in q.protrudes)
            {
                if (edge.v0 == s || edge.v1 == s)
                    return edge;
            }
            return null;
        }

    
        public static IEnumerable<Vector2> RelaxPoints(IEnumerable<Vector2> startingPoints, float width, float height)
        {
            Delaunay.Voronoi v = new Delaunay.Voronoi(startingPoints.ToList(), null, new Rect(0, 0, width, height));
            foreach (var point in startingPoints)
            {
                var region = v.Region(point);
                point.Set(0, 0);
                foreach (var r in region)
                    point.Set(point.x + r.x, point.y + r.y);

                point.Set(point.x / region.Count, point.y / region.Count);
                yield return point;
            }
        }
    }
}
