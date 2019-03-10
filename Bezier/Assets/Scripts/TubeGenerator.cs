using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CustomMesh
{
    public List<Vector3> vertices;
    public List<int> triangles;
    public List<Vector2> uv;

    public CustomMesh()
    {
        vertices = new List<Vector3>();
        uv = new List<Vector2>();
        triangles = new List<int>();
    }

    public void GenerateQuade()
    {
        var _vertices = new Vector3[4];
        var _triangles = new int[6];
        var _uv = new Vector2[4];

        _vertices[0] = new Vector3(0.5f, 0, 0.5f);
        _vertices[1] = new Vector3(-0.5f, 0, 0.5f);
        _vertices[2] = new Vector3(-0.5f, 0, -0.5f);
        _vertices[3] = new Vector3(0.5f, 0, -0.5f);

        _uv[0] = new Vector2(1, 1);
        _uv[1] = new Vector2(0, 1);
        _uv[2] = new Vector2(0, 0);
        _uv[3] = new Vector2(1, 0);

        _triangles[0] = 0;
        _triangles[1] = 1;
        _triangles[2] = 3;
        _triangles[3] = 3;
        _triangles[4] = 1;
        _triangles[5] = 2;

        vertices = new List<Vector3>(_vertices);
        triangles = new List<int>(_triangles);
        uv = new List<Vector2>(_uv);
    }

    public void Transformation(Matrix4x4 matrix)
    {
        for(var i=0; i<vertices.Count; i++)
        {
            vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
        }
    }

    public static CustomMesh operator +(CustomMesh c1, CustomMesh c2)
    {
        var result = new CustomMesh();
        var vertex1Count = c1.vertices.Count;
        result.vertices.AddRange(c1.vertices);
        result.vertices.AddRange(c2.vertices);

        result.uv.AddRange(c1.uv);
        result.uv.AddRange(c2.uv);

        result.triangles.AddRange(c1.triangles);
        result.triangles.AddRange(c2.triangles.Select(t=>t+vertex1Count));

        return result;
    }

    public void Optimize()
    {
        Vector3 v;
        Vector3 v2;
        for (int i = 0; i < vertices.Count; i++)
        {
            v = vertices[i];
            for (int j = i + 1; j < vertices.Count; j++)
            {
                v2 = vertices[j];
                if(Vector3.Distance(v, v2)>0.001f)
                    continue;

                for (int ind = 0; ind < triangles.Count; ind++)
                {
                    if (triangles[ind] == j)
                    {
                        triangles[ind] = i;
                    }
                    else if (triangles[ind] > j && triangles[ind] > 0)
                    {
                        triangles[ind]--;
                    }
                }

                vertices.RemoveAt(j);
                uv.RemoveAt(j);
            }
        }
    }

    public void ModifyByCurve(BezierCurve curve, bool uniform=true)
    {
        int i = 0;
        var currentLength = 0f;
        var lastPoint = curve[0].position;
        foreach (var vertex in vertices)
        {
            var l = vertex.z/curve.length;
            var t = uniform ? curve.GetUniformPersentAtT(l) : l;
            var point = curve.GetPointAt(t);
            currentLength += Vector3.Distance(point, lastPoint);
            
            var dir = curve.GetDirection(t);
            lastPoint = point;

            Quaternion rot = Quaternion.LookRotation(dir);
            Matrix4x4 m = Matrix4x4.TRS(point, rot, Vector3.one);

            var pos = new Vector3(vertex.x, vertex.y, 0);
            float u = Vector3.Angle(pos, Vector3.up)/360f * Mathf.Sign(pos.x);// Vector3.Dot(pos, Vector3.up) * Mathf.Sign(pos.y);
            uv[i] = new Vector2(u, currentLength);
            vertices[i] = m.MultiplyPoint3x4(pos);
            i++;
        }
    }

};

public class TubeGenerator : MonoBehaviour
{
    [SerializeField] private BezierCurve spline;

    [Range(3, 20)]
    [SerializeField] private int facetCount = 4;
    [SerializeField] private int segmentCount = 30;
    [SerializeField] private float radius = 4;
    [SerializeField] private Material material;
    void Start ()
    {
        
    }

    [ContextMenu("Generate")]
    private void Generate()
    {
        var length = spline.length;
        var cmesh = CreateTube(Vector3.zero, length, segmentCount);
        cmesh.Optimize();
        cmesh.ModifyByCurve(spline, false);
        var go = new GameObject("tube");
        var filter = go.AddComponent<MeshFilter>();
        var render = go.AddComponent<MeshRenderer>();
        render.sharedMaterial = material;
        var mesh = new Mesh();
        mesh.vertices = cmesh.vertices.ToArray();
        mesh.triangles = cmesh.triangles.ToArray();
        mesh.uv = cmesh.uv.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.UploadMeshData(true);
        filter.sharedMesh = mesh;
    }

    public CustomMesh CreateTube(Vector3 start, float length, int segmentCount)
    {
        var mesh = new CustomMesh();
        for (var i = 0; i < segmentCount; i++)
        {
            var seqment = CreateSection(start + Vector3.forward *i*length/segmentCount, length/segmentCount);
            mesh += seqment;
        }
        return mesh;
    }

    private CustomMesh CreateSection(Vector3 center, float length)
    {
        var mesh = new CustomMesh();
        for( var i = 0; i < facetCount; i++ )
        {
            var angleSegment = Mathf.PI * 2f / facetCount;
            var angle = i * angleSegment;
            var vertex = new Vector3(radius*Mathf.Cos(angle), radius*Mathf.Sin(angle), 0);
            vertex += center;
            mesh.vertices.Add(vertex);
            mesh.uv.Add(Vector2.zero);
        }

        for (var i = 0; i < facetCount; i++)
        {
            var angleSegment = Mathf.PI * 2f / facetCount;
            var angle = i * angleSegment;
            var vertex = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), length);
            vertex += center;
            mesh.vertices.Add(vertex);
            mesh.uv.Add(Vector2.zero);
        }

        int count = mesh.vertices.Count;
        for ( var i = 0; i < facetCount; i++ )
        {
            mesh.triangles.Add(i);
            mesh.triangles.Add(facetCount + i);
            var j = i == 0 ? count - 1 : facetCount + i - 1;
            mesh.triangles.Add(j);

            mesh.triangles.Add(i);
            mesh.triangles.Add(j);
            j = i == 0 ? facetCount - 1 : i - 1;
            mesh.triangles.Add(j);
        }
        return mesh;
    }

    
};
