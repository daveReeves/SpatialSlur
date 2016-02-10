﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialSlur.SlurCore;


namespace SpatialSlur.SlurMesh
{
    /// <summary>
    /// Fixed - All vertices on the boundary vertices are fixed.
    /// CornerFixed - Degree 2 boundary vertices are fixed. Other boundary vertices consider neighbours which are also on the boundary.
    /// Free - Boundary vertices consider neighbours which are also on the boundary.
    /// </summary>
    public enum SmoothBoundaryType
    {
        Fixed,
        CornerFixed,
        Free
    }


    /// <summary>
    /// 
    /// </summary>
    public static class HeSubdivide
    {
        // Delegates for different SmoothBoundary types
        private static Action<HeVertexList, int>[] _ccMoveOldVerts = 
        { 
            CCMoveOldVertsFixed, 
            CCMoveOldVertsCornerFixed, 
            CCMoveOldVertsFree 
        };


        /// <summary>
        /// Applies a single iteration of Catmull-Clark subdivision to the given mesh.
        /// http://rosettacode.org/wiki/Catmull%E2%80%93Clark_subdivision_surface
        /// http://w3.impa.br/~lcruz/courses/cma/surfaces.html
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="boundaryType"></param>
        public static void CatmullClark(HeMesh mesh, SmoothBoundaryType boundaryType)
        {
            var verts = mesh.Vertices;
            var edges = mesh.HalfEdges;
            var faces = mesh.Faces;
            int nv = verts.Count;
            int ne = edges.Count;
            int nf = faces.Count;

            // create face vertices (1 new vertex per face)
            for (int i = 0; i < nf; i++)
            {
                HeFace f = faces[i];
                if (f.IsUnused)
                    verts.Add(new Vec3d()); // add dummy vertex for unused elements
                else
                    verts.Add(f.GetCenter());
            }

            // create edge vertices (1 new vertex per edge pair)
            for (int i = 0; i < ne; i += 2)
            {
                HalfEdge e = edges[i];
                if (e.IsUnused)
                    verts.Add(new Vec3d()); // add dummy vertex for unused elements
                else
                    edges.SplitEdge(e); // split edge (adds a new vertex at edge center)
            }

            // update old vertex positions (this depends on boundary type)
            _ccMoveOldVerts[(int)boundaryType](verts, nv);

            // update edge vertex positions (loop through all new edge pairs)
            for (int i = nv + nf; i < verts.Count; i++)
            {
                HeVertex v = verts[i];
                HalfEdge e = v.First;
                if (e.Face == null) continue;

                HeVertex fv0 = verts[e.Face.Index + nv];
                HeVertex fv1 = verts[e.Twin.Face.Index + nv];
                v.Position = ((fv0.Position + fv1.Position) * 0.5 + v.Position) * 0.5;
            }

            // create new edges and faces
            for (int i = 0; i < nf; i++)
            {
                HeFace f = faces[i];
                if (f.IsUnused) continue;
                HeVertex fv = verts[nv + i]; // face vertex

                // ensure first edge in face starts at an old vertex
                if (f.First.Start.Index >= nv)
                    f.First.Previous.MakeFirstInFace();

                // create new edges to face centers and link up to old edges
                HalfEdge e0 = f.First.Next;
                HeVertex v0 = e0.Start;
                do
                {
                    HalfEdge e1 = edges.AddPair(e0.Start, fv);
                    HalfEdge.MakeConsecutive(e0.Previous, e1);
                    HalfEdge.MakeConsecutive(e1.Twin, e0);
                    e0 = e0.Next.Next;
                } while (e0.Start != v0);
                fv.First = e0.Twin; // set outgoing edge for the face vertex

                // connect new edges to eachother and create new faces where necessary
                e0 = f.First;
                v0 = e0.Start;
                do
                {
                    HalfEdge e1 = e0.Previous;
                    HalfEdge e2 = e0.Next;
                    HalfEdge e3 = e1.Previous;
                    HalfEdge.MakeConsecutive(e2, e3);

                    // create new face if necessary
                    if (f == null)
                    {
                        f = new HeFace();
                        faces.Add(f);
                        f.First = e0;
                        e0.Face = e1.Face = f; // update face refs for old edges
                    }

                    e2.Face = e3.Face = f; // set face refs for new edges
                    f = null;
                    e0 = e2.Twin.Next.Next;
                } while (e0.Start != v0);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="count"></param>
        private static void CCMoveOldVertsFixed(HeVertexList vertices, int count)
        {
            for (int i = 0; i < count; i++)
            {
                HeVertex v = vertices[i];
                if (v.IsUnused || v.IsBoundary) continue;

                Vec3d favg = new Vec3d();
                Vec3d eavg = new Vec3d();
                int n = 0;

                foreach (HalfEdge e in v.OutgoingHalfEdges)
                {
                    favg += vertices[e.Face.Index + count].Position;
                    eavg += e.End.Position;
                    n++;
                }

                double t = 1.0 / n;
                v.Position = (t * favg + t * 2 * eavg + (n - 3) * v.Position) * t;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="count"></param>
        private static void CCMoveOldVertsCornerFixed(HeVertexList vertices, int count)
        {
            for (int i = 0; i < count; i++)
            {
                HeVertex v = vertices[i];
                if (v.IsUnused) continue;

                if (v.IsBoundary)
                {
                    HalfEdge e = v.First;
                    if (!e.IsFromDegree2) v.Position = 0.25 * e.End.Position + 0.25 * e.Previous.Start.Position + 0.5 * v.Position;
                }
                else
                {
                    Vec3d favg = new Vec3d();
                    Vec3d eavg = new Vec3d();
                    int n = 0;

                    foreach (HalfEdge e in v.OutgoingHalfEdges)
                    {
                        favg += vertices[e.Face.Index + count].Position;
                        eavg += e.End.Position;
                        n++;
                    }

                    double t = 1.0 / n;
                    v.Position = (t * favg + t * 2 * eavg + (n - 3) * v.Position) * t;
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="count"></param>
        private static void CCMoveOldVertsFree(HeVertexList vertices, int count)
        {
            for (int i = 0; i < count; i++)
            {
                HeVertex v = vertices[i];
                if (v.IsUnused) continue;

                if (v.IsBoundary)
                {
                    HalfEdge e = v.First;
                    v.Position = 0.25 * e.End.Position + 0.25 * e.Previous.Start.Position + 0.5 * v.Position;
                }
                else
                {
                    Vec3d favg = new Vec3d();
                    Vec3d eavg = new Vec3d();
                    int n = 0;

                    foreach (HalfEdge e in v.OutgoingHalfEdges)
                    {
                        favg += vertices[e.Face.Index + count].Position;
                        eavg += e.End.Position;
                        n++;
                    }

                    double t = 1.0 / n;
                    v.Position = (t * favg + t * 2 * eavg + (n - 3) * v.Position) * t;
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="count"></param>
        private static void CCMoveOldVertsCrease(HeVertexList vertices, int count, IList<int> edgeStatus)
        {
            for (int i = 0; i < count; i++)
            {
                HeVertex v = vertices[i];
                if (v.IsUnused) continue;

                /*
                foreach(HalfEdge e in v.OutgoingHalfEdges)
                {

                }
                */

                if (v.IsBoundary)
                {
                    HalfEdge e = v.First;
                    v.Position = 0.25 * e.End.Position + 0.25 * e.Previous.Start.Position + 0.5 * v.Position;
                }
                else
                {
                    Vec3d favg = new Vec3d();
                    Vec3d eavg = new Vec3d();
                    int n = 0;

                    foreach (HalfEdge e in v.OutgoingHalfEdges)
                    {
                        favg += vertices[e.Face.Index + count].Position;
                        eavg += e.End.Position;
                        n++;
                    }

                    double t = 1.0 / n;
                    v.Position = (t * favg + t * 2 * eavg + (n - 3) * v.Position) * t;
                }
            }
        }


        /// <summary>
        /// Applies a single iteration of QuadSplit subdivision. 
        /// This is effectively CatmullClark without the smoothing.
        /// </summary>
        /// <param name="mesh"></param>
        public static void QuadSplit(HeMesh mesh)
        {
            var verts = mesh.Vertices;
            var edges = mesh.HalfEdges;
            var faces = mesh.Faces;
            int nv = verts.Count;
            int ne = edges.Count;
            int nf = faces.Count;

            // create face vertices (1 new vertex per face)
            for (int i = 0; i < nf; i++)
            {
                HeFace f = faces[i];
                if (f.IsUnused)
                    verts.Add(new Vec3d()); // add dummy vertex for unused elements
                else
                    verts.Add(f.GetCenter());
            }

            // create edge vertices (1 new vertex per edge pair)
            for (int i = 0; i < ne; i += 2)
            {
                HalfEdge e = edges[i];
                if (e.IsUnused)
                    verts.Add(new Vec3d()); // add dummy vertex for unused elements
                else
                    edges.SplitEdge(e); // split edge (adds a new vertex at edge center)
            }

            // create new edges and faces
            for (int i = 0; i < nf; i++)
            {
                HeFace f = faces[i];
                if (f.IsUnused) continue;
                HeVertex fv = verts[nv + i]; // face vertex

                // ensure first edge in face starts at an old vertex
                if (f.First.Start.Index >= nv)
                    f.First.Previous.MakeFirstInFace();

                // create new edges to face centers and link up to old edges
                HalfEdge e0 = f.First.Next;
                HeVertex v0 = e0.Start;
                do
                {
                    HalfEdge e1 = edges.AddPair(e0.Start, fv);
                    HalfEdge.MakeConsecutive(e0.Previous, e1);
                    HalfEdge.MakeConsecutive(e1.Twin, e0);
                    e0 = e0.Next.Next;
                } while (e0.Start != v0);
                fv.First = e0.Twin; // set outgoing edge for the face vertex

                // connect new edges to eachother and create new faces where necessary
                e0 = f.First;
                v0 = e0.Start;
                do
                {
                    HalfEdge e1 = e0.Previous;
                    HalfEdge e2 = e0.Next;
                    HalfEdge e3 = e1.Previous;
                    HalfEdge.MakeConsecutive(e2, e3);

                    // create new face if necessary
                    if (f == null)
                    {
                        f = new HeFace();
                        faces.Add(f);
                        f.First = e0;
                        e0.Face = e1.Face = f; // update face refs for old edges
                    }

                    e2.Face = e3.Face = f; // set face refs for new edges
                    f = null;
                    e0 = e2.Twin.Next.Next;
                } while (e0.Start != v0);
            }
        }


        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="mesh"></param>
        public static void Loop(HeMesh mesh)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="mesh"></param>
        public static void Stellate(HeMesh mesh)
        {
            HeFaceList faces = mesh.Faces;
            int nf = faces.Count;

            for (int i = 0; i < nf; i++)
            {
                HeFace f = faces[i];
                if (f.IsUnused) continue;
                faces.Stellate(f);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="faceCenters"></param>
        /// <param name="faceValues"></param>
        /// <returns></returns>
        public static HeMesh DualSkeleton(HeMesh mesh, IList<Vec3d> halfEdgePoints)
        {
            HeFaceList faces = mesh.Faces;
            HalfEdgeList edges = mesh.HalfEdges;

            edges.SizeCheck(halfEdgePoints);
            HeMesh result = new HeMesh();

            // add one new vertex per edge
            for (int i = 0; i < edges.Count; i++)
                result.Vertices.Add(halfEdgePoints[i]);

            // add nodal faces
            for (int i = 0; i < faces.Count; i++)
            {
                HeFace f = faces[i];

                // collect indices of face edges
                List<int> fi = new List<int>();
                foreach (HalfEdge e in f.HalfEdges)
                    fi.Add(e.Index);

                result.Faces.Add(fi);
            }

            // add edge faces
            for (int i = 0; i < edges.Count; i += 2)
            {
                HalfEdge e0 = edges[i];
                if (e0.IsBoundary) continue;

                HalfEdge e1 = e0.Twin;
                result.Faces.Add(e0.Next.Index, e0.Index, e1.Next.Index, e1.Index);
            }

            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="faceCenters"></param>
        /// <param name="faceValues"></param>
        /// <returns></returns>
        public static HeMesh DualSkeleton(HeMesh mesh, IList<Vec3d> faceCenters, IList<double> faceValues)
        {
            HeFaceList faces = mesh.Faces;
            HalfEdgeList edges = mesh.HalfEdges;

            faces.SizeCheck(faceCenters);
            faces.SizeCheck(faceValues);

            HeMesh result = new HeMesh();

            // add one new vertex per edge
            for (int i = 0; i < edges.Count; i++)
            {
                HalfEdge e = edges[i];
                HeFace f = e.Face;
                Vec3d p = e.Start.Position;

                if (f == null)
                {
                    result.Vertices.Add(p);
                }
                else
                {
                    int fi = f.Index;
                    result.Vertices.Add(Vec3d.Lerp(p, faceCenters[fi], faceValues[fi]));
                }
            }

            // add nodal faces
            for (int i = 0; i < faces.Count; i++)
            {
                HeFace f = faces[i];

                // collect indices of face edges
                List<int> fi = new List<int>();
                foreach (HalfEdge e in f.HalfEdges)
                    fi.Add(e.Index);

                result.Faces.Add(fi);
            }

            // add edge faces
            for (int i = 0; i < edges.Count; i += 2)
            {
                HalfEdge e0 = edges[i];
                if (e0.IsBoundary) continue;

                HalfEdge e1 = e0.Twin;
                result.Faces.Add(e0.Next.Index, e0.Index, e1.Next.Index, e1.Index);
            }

            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="faceCenters"></param>
        /// <param name="faceValues"></param>
        /// <returns></returns>
        public static HeMesh DualSkeleton2(HeMesh mesh, IList<Vec3d> faceCenters, IList<double> faceValues)
        {
            HeFaceList faces = mesh.Faces;
            HalfEdgeList edges = mesh.HalfEdges;

            faces.SizeCheck(faceCenters);
            faces.SizeCheck(faceValues);

            HeMesh result = new HeMesh();

            // add one new vertex per face
            for (int i = 0; i < faces.Count; i++)
                result.Vertices.Add(faceCenters[i]);

            // add one new vertex per edge
            for (int i = 0; i < edges.Count; i++)
            {
                HalfEdge e = edges[i];
                HeFace f = e.Face;
                Vec3d p = e.Start.Position;

                if (f == null)
                {
                    result.Vertices.Add(p);
                }
                else
                {
                    int fi = f.Index;
                    result.Vertices.Add(Vec3d.Lerp(p, faceCenters[fi], faceValues[fi]));
                }
            }

            // add faces
            int nf = faces.Count;
            for (int i = 0; i < edges.Count; i += 2)
            {
                HalfEdge e0 = edges[i];
                if (e0.IsBoundary) continue;

                HalfEdge e1 = e0.Twin;
                HeFace f0 = e0.Face;
                HeFace f1 = e1.Face;

                result.Faces.Add(f0.Index, e0.Index + nf, e1.Next.Index + nf, f1.Index);
                result.Faces.Add(f1.Index, e1.Index + nf, e0.Next.Index + nf, f0.Index);
            }

            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="faceCenters"></param>
        /// <param name="faceValues"></param>
        /// <returns></returns>
        public static HeMesh DualSkeleton2(HeMesh mesh, IList<Vec3d> faceCenters, IList<Vec3d> halfEdgePoints)
        {
            HeFaceList faces = mesh.Faces;
            HalfEdgeList edges = mesh.HalfEdges;

            faces.SizeCheck(faceCenters);
            edges.SizeCheck(halfEdgePoints);

            HeMesh result = new HeMesh();

            // add one new vertex per face
            for (int i = 0; i < faces.Count; i++)
                result.Vertices.Add(faceCenters[i]);

            // add one new vertex per edge
            for (int i = 0; i < edges.Count; i++)
                result.Vertices.Add(halfEdgePoints[i]);

            // add faces
            int nf = faces.Count;
            for (int i = 0; i < edges.Count; i += 2)
            {
                HalfEdge e0 = edges[i];
                if (e0.IsBoundary) continue;

                HalfEdge e1 = e0.Twin;
                HeFace f0 = e0.Face;
                HeFace f1 = e1.Face;

                result.Faces.Add(f0.Index, e0.Index + nf, e1.Next.Index + nf, f1.Index);
                result.Faces.Add(f1.Index, e1.Index + nf, e0.Next.Index + nf, f0.Index);
            }

            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="halfEdgePoints"></param>
        /// <returns></returns>
        public static HeMesh BevelEdges(HeMesh mesh, IList<Vec3d> halfEdgePoints)
        {
            HalfEdgeList edges = mesh.HalfEdges;
            edges.SizeCheck(halfEdgePoints);

            HeVertexList verts = mesh.Vertices;
            int ne = edges.Count;
            int nv = verts.Count;

            HeMesh result = new HeMesh();
            HeVertexList newVerts = result.Vertices;
            HeFaceList newFaces = result.Faces;

            // add vertices (1 per halfedge in the original mesh)
            for (int i = 0; i < ne; i++)
                newVerts.Add(halfEdgePoints[i]);

            // add edge faces (1 per edge in the original mesh)
            for (int i = 0; i < ne; i += 2)
            {
                HalfEdge e = edges[i];
                int i1 = e.Twin.Next.Index;
                int i2 = e.Twin.Index;
                int i3 = e.Next.Index;
                newFaces.Add(i, i1, i2, i3);
            }

            // add node faces (1 per node in the original mesh)
            for (int i = 0; i < nv; i++)
            {
                HeVertex v = verts[i];
                List<int> ids = new List<int>();

                // circulate vertex in reverse for consistent face windings
                HalfEdge e = v.First;
                do
                {
                    ids.Add(e.Index);
                    e = e.Previous.Twin;
                } while (e != v.First);

                newFaces.Add(ids);
            }

            return result;
        }

    }
}
