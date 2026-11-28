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
    internal static Mesh Ribbon(List<Vector3> centers,bool positiveSide)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();
        const int resolution=24,sides=8;
        for(int i=0;i<(centers.Count-1)*resolution+1;i++)
        {
            int segment=Mathf.Min(i/resolution,centers.Count-2);float fraction=(i-segment*resolution)/(float)resolution;
            Vector3 center=Vector3.Lerp(centers[segment],centers[segment+1],fraction);
            // Grid-aligned cross sections: each cell is straight or one full diagonal.
            Vector3 tangent=centers[centers.Count-1]-centers[0];
            Vector3 lateral=Mathf.Abs(tangent.x)>=Mathf.Abs(tangent.z)?Vector3.forward:Vector3.left;
            float radius=1f;
            for(int n=0;n<=sides;n++)
            {
                float angle=(positiveSide?-.5f:.5f)*Mathf.PI+n*Mathf.PI/sides;
                vertices.Add(center+lateral*(Mathf.Cos(angle)*radius)+Vector3.up*(Mathf.Sin(angle)*(.25f+.06f*Mathf.Cos(i*Mathf.PI*2/3f))));
            }
            if(i==0)continue;
            for(int n=0;n<sides;n++)
            {int a=(i-1)*(sides+1)+n,b=i*(sides+1)+n;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
        }
        return Finish(vertices,triangles,"Continuous pleated hose");
    }
    internal static Mesh Spikes(Vector2 outward, Vector2? edge=null)
    {
        Vector2 tangent=edge??new Vector2(-outward.y,outward.x);
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
