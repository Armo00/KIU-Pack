using UnityEngine;
namespace KIU.NetRecovery {
 // A lightweight round cable; one material, one submesh, no physics bodies.
 public sealed class CableVisual {
  public readonly GameObject gameObject;readonly Mesh mesh;readonly MeshRenderer renderer;
  readonly Vector3[] positions=new Vector3[33],vertices=new Vector3[33*8],normals=new Vector3[33*8];
  public bool enabled {get{return renderer.enabled;}set{renderer.enabled=value;}}
  public CableVisual(Transform parent,string name,Material material){
   gameObject=new GameObject(name);gameObject.layer=parent.gameObject.layer;gameObject.transform.SetParent(parent,false);
   mesh=new Mesh();mesh.name=name;mesh.MarkDynamic();var f=gameObject.AddComponent<MeshFilter>();f.sharedMesh=mesh;
   renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
   var triangles=new int[32*8*6];int k=0;
   for(int j=0;j<32;j++)for(int s=0;s<8;s++){int a=j*8+s,b=j*8+(s+1)%8,c=(j+1)*8+s,d=(j+1)*8+(s+1)%8;triangles[k++]=a;triangles[k++]=b;triangles[k++]=c;triangles[k++]=b;triangles[k++]=d;triangles[k++]=c;}
   mesh.vertices=vertices;mesh.triangles=triangles;
  }
  public Vector3 GetPosition(int i){return positions[i];}
  public void SetPositions(Vector3[] input,float radius){
   for(int i=0;i<33;i++)positions[i]=input[i];
   for(int i=0;i<33;i++){
    Vector3 tangent=input[Mathf.Min(32,i+1)]-input[Mathf.Max(0,i-1)];if(tangent.sqrMagnitude<1e-10f)tangent=Vector3.right;tangent.Normalize();
    Vector3 side=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.95f?Vector3.right:Vector3.up).normalized,up=Vector3.Cross(tangent,side).normalized;
    for(int s=0;s<8;s++){float a=s*Mathf.PI/4;Vector3 normal=side*Mathf.Cos(a)+up*Mathf.Sin(a);normals[i*8+s]=normal;vertices[i*8+s]=input[i]+normal*radius;}
   }
   mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();
  }
  public void Destroy(){Object.Destroy(mesh);Object.Destroy(gameObject);}
 }
}
