using System.Collections.Generic;
using UnityEngine;

// Geometry is generated locally; gameplay keeps using the grid colliders.
internal static class FanhuaPaperMesh
{
    internal static Mesh Pleats()
    {
        var v=new List<Vector3>();var t=new List<int>();
        // Open diamond cells, alternating depth: actual folded sheets rather than cubes.
        for(int i=0;i<8;i++)
        {
            float x=-.48f+i*.12f;
            Quad(new Vector3(x,0,-.48f),new Vector3(x+.06f,.44f,0),new Vector3(x+.12f,0,-.48f),new Vector3(x+.06f,-.3f,0));
            Quad(new Vector3(x,0,.48f),new Vector3(x+.06f,-.3f,0),new Vector3(x+.12f,0,.48f),new Vector3(x+.06f,.44f,0));
        }
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {int n=v.Count;v.AddRange(new[]{a,b,c,d});t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
        return Finish(v,t,"Paper honeycomb folds");
    }
    private static Mesh Finish(List<Vector3> vertices,List<int> triangles,string name)
    {
        // Back faces need independent vertices; otherwise recalculated normals cancel.
        int count=vertices.Count,indexCount=triangles.Count;
        vertices.AddRange(vertices.ToArray());
        for(int i=0;i<indexCount;i+=3)triangles.AddRange(new[]{triangles[i]+count,triangles[i+2]+count,triangles[i+1]+count});
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();return mesh;
    }
    internal static Mesh Spikes(Vector2 outward)
    {
        Vector2 tangent=new Vector2(-outward.y,outward.x);
        var v=new List<Vector3>();var triangles=new List<int>();
        for(int i=0;i<4;i++)
        {
            float a=-.5f+i*.25f;int n=v.Count;
            foreach(var p in new[]{tangent*a-outward*.5f,tangent*(a+.125f)+outward*.48f,tangent*(a+.25f)-outward*.5f})v.Add(new Vector3(p.x,0,p.y));
            triangles.AddRange(new[]{n,n+1,n+2});
        }
        return Finish(v,triangles,"Outward paper spikes");
    }
}
