using System;
namespace KIU.NetRecovery {
 // A small quasi-static string network. Fixed ends and hook mouths, four
 // unilateral rope/rope contacts. No Rigidbody chain or force injection.
 public sealed class CableGeometry {
  public const int Samples=33;
  public readonly Point3[][] points=new Point3[4][];
  public readonly int[] mouthIndex=new int[4];
  public readonly double[] lengths=new double[4];
  readonly double[,] coordinate=new double[4,5],height=new double[4,5];
  readonly bool[,] pinned=new bool[4,5];
  readonly int[,] crossing=new int[4,2];
  readonly int[,] roles=new int[4,5];
  readonly double[] offsets=new double[4];
  readonly int[] order=new int[4];bool orderSet;
  public double minimumCrossingGap;
  public CableGeometry(){for(int i=0;i<4;i++)points[i]=new Point3[Samples];}
  public void ResetOrder(){orderSet=false;}
  public void ChooseOrder(Point3[] mouths,double plane,double span,double centerZ){
   for(int x=0;x<2;x++)for(int z=0;z<2;z++){
    double a=NaturalHeight(mouths[x].x,mouths[x].y,mouths[z+2].x,-span,span,plane+.3);
    double b=NaturalHeight(mouths[z+2].z,mouths[z+2].y,mouths[x].z,centerZ-span,centerZ+span,plane-.3);
    order[x*2+z]=a>=b?1:-1;
   }orderSet=true;
  }
  static double NaturalHeight(double hookAlong,double hookY,double along,double lo,double hi,double endY){return along<=hookAlong?Lerp(endY,hookY,(along-lo)/Math.Max(.00001,hookAlong-lo)):Lerp(hookY,endY,(along-hookAlong)/Math.Max(.00001,hi-hookAlong));}
  public void Solve(Point3[] mouths,double[] railOffsets,bool[] held,double plane,double span,double centerZ,double separation){
   if(!orderSet){bool all=true;for(int i=0;i<4;i++)all&=held[i];if(all)ChooseOrder(mouths,plane,span,centerZ);else for(int i=0;i<4;i++)order[i]=1;}
   for(int r=0;r<4;r++){
    bool x=r<2;double origin=x?0:centerZ;offsets[r]=railOffsets[r];
    double along=x?mouths[r].x:mouths[r].z;
    coordinate[r,0]=origin-span;coordinate[r,1]=held[r]?along:origin;
    coordinate[r,2]=x?railOffsets[2]:centerZ+railOffsets[0];coordinate[r,3]=x?railOffsets[3]:centerZ+railOffsets[1];coordinate[r,4]=origin+span;
    for(int j=0;j<5;j++)roles[r,j]=j;
    for(int a=1;a<5;a++)for(int b=a;b>0&&coordinate[r,b]<coordinate[r,b-1];b--){double d=coordinate[r,b];coordinate[r,b]=coordinate[r,b-1];coordinate[r,b-1]=d;int role=roles[r,b];roles[r,b]=roles[r,b-1];roles[r,b-1]=role;}
    double endHeight=plane+(r<2?.3:-.3);
    for(int j=0;j<5;j++){
     int role=roles[r,j];pinned[r,j]=role==0||role==4||(role==1&&held[r]);
     double t=coordinate[r,j];height[r,j]=endHeight;
     if(held[r])height[r,j]=t<=along?Lerp(endHeight,mouths[r].y,(t-origin+span)/Math.Max(.00001,along-origin+span)):Lerp(mouths[r].y,endHeight,(t-along)/Math.Max(.00001,origin+span-along));
     if(role==1)mouthIndex[r]=j*8;
     if(role==2||role==3)crossing[r,role-2]=j;
     // A crossover coincident with a hook is fixed by that actual hook.
     if(held[r]&&Math.Abs(t-along)<.002){pinned[r,j]=true;height[r,j]=mouths[r].y;}
    }
   }
   for(int iteration=0;iteration<120;iteration++){
    for(int r=0;r<4;r++)for(int j=1;j<4;j++)if(!pinned[r,j]){
     double a=1/Math.Max(.002,coordinate[r,j]-coordinate[r,j-1]),b=1/Math.Max(.002,coordinate[r,j+1]-coordinate[r,j]);
     height[r,j]=(a*height[r,j-1]+b*height[r,j+1])/(a+b);
    }
    for(int x=0;x<2;x++)for(int z=0;z<2;z++){
     int xi=crossing[x,z],zi=crossing[z+2,x],sign=order[x*2+z];double missing=separation-sign*(height[x,xi]-height[z+2,zi]);
     if(missing<=0)continue;
     double wx=pinned[x,xi]?0:Compliance(x,xi),wz=pinned[z+2,zi]?0:Compliance(z+2,zi),sum=wx+wz;
     if(sum>0){height[x,xi]+=sign*missing*wx/sum;height[z+2,zi]-=sign*missing*wz/sum;}
    }
   }
   minimumCrossingGap=Double.PositiveInfinity;
   for(int x=0;x<2;x++)for(int z=0;z<2;z++)minimumCrossingGap=Math.Min(minimumCrossingGap,order[x*2+z]*(height[x,crossing[x,z]]-height[z+2,crossing[z+2,x]]));
   for(int r=0;r<4;r++){
    lengths[r]=0;
    for(int j=0;j<Samples;j++){
     int segment=Math.Min(3,j/8);double u=(j-segment*8)/8d;
     double along=Lerp(coordinate[r,segment],coordinate[r,segment+1],u),y=Lerp(height[r,segment],height[r,segment+1],u);
     // Small self-weight bow only; concentrated reactions are at real hooks
     // and rope contacts. Both meeting cables keep their prescribed diameter.
     double bow=Math.Min(.025,Math.Abs(coordinate[r,segment+1]-coordinate[r,segment])*.001);
     y-=4*bow*u*(1-u);
     points[r][j]=r<2?new Point3(along,y,centerZ+offsets[r]):new Point3(offsets[r],y,along);
     if(j>0){var a=points[r][j-1];var b=points[r][j];lengths[r]+=Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));}
    }
   }
  }
  double Compliance(int r,int j){if(j==0||j==4)return 0;return 1/(1/Math.Max(.002,coordinate[r,j]-coordinate[r,j-1])+1/Math.Max(.002,coordinate[r,j+1]-coordinate[r,j]));}
  static double Lerp(double a,double b,double t){return a+(b-a)*Math.Max(0,Math.Min(1,t));}
 }
}
