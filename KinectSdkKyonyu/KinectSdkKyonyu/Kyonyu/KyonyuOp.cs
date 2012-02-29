using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace KinectSdkKyonyu.Kyonyu
{
    using Touch = Tuple<Vector3, float>;
using Microsoft.Xna.Framework.Graphics;
    class KyonyuOp : KyonyuMesh
    {
        public static readonly int COLS = 12;
        public static readonly int ROWS = 6;
        public static readonly float RADIUS = 90;//[mm]
        public static readonly float WARP_LENGTH = 100;//[mm]
        private List<KyonyuJoint> m_JointList;
        private List<KyonyuPoint> m_PointList;
        Matrix m_PinnedMatrix=Matrix.Identity;
        List<Touch> m_TouchingList=new List<Touch>();
        bool m_IsTouched=false;

	    private int getOpCount()
	    {
            return COLS * (ROWS - 1) + 1+1;//周囲＋極＋中心
	    }
        private int getOpIndex(int inY,int inX)
        {
            if (inY >= ROWS-1)
            {
                return inY * COLS;
            }
            else
            {
                return inY * COLS + (COLS + inX) % COLS;
            }
        }
        private void putPointAndJoint()
        {
            //Xを経度
            //Yを緯度
            
            float diffRotX = MathHelper.ToRadians( 360.0f / COLS);
            float diffRotY = MathHelper.ToRadians(90.0f / (ROWS - 1));

            m_PointList = new List<KyonyuPoint>();
            m_JointList = new List<KyonyuJoint>();
            List<int> indexList=new List<int>();

            for (int i = 0; i < getOpCount(); ++i)
            {
                m_PointList.Add(new KyonyuPoint());
            }


            //ポイントとジョイントを一気に配置したほうが楽なので
            for (int y = 0; y < ROWS; ++y)
            {
                for (int x = 0; x < COLS; ++x)
                {
                    //ref
                    KyonyuPoint  point = m_PointList[getOpIndex(y, x)];
                    //point.name = String(y) + "-" + String(x) + " ";//デバッグ用
                    //var noize:Number = Math.random()*10;//0から1
                    //よこ
                    //var tmp:MyVector3D = new MyVector3D(0,Math.cos(diffRotX*x),Math.sin(diffRotX*x));
                    //point._position = new MyVector3D(Math.sin(diffRotY * y) * RADIUS, Math.cos(diffRotY*y)*tmp.y*RADIUS, Math.cos(diffRotY*y)*tmp.z*RADIUS);
                    Vector3 tmp = new Vector3((float)Math.Sin(diffRotX * x), (float)Math.Cos(diffRotX * x), 0);
                    point.m_Position = new Vector3(
                                                 (float)Math.Cos(diffRotY * y) * tmp.X * RADIUS,
                                                 (float)Math.Cos(diffRotY * y) * tmp.Y * RADIUS,
                                                 (float)Math.Sin(diffRotY * y) * RADIUS
                                                 );
                    if (y == ROWS - 1)
                    {
                        point.m_Position.Z *= 1.1f;
                    }


                    //面
                    if (y > 0)
                    {
                        indexList.Add(getOpIndex(y - 1, x - 1));
                        indexList.Add(getOpIndex(y - 1, x + 0));
                        indexList.Add(getOpIndex(y + 0, x - 1));

                        if (y < ROWS - 1)
                        {
                            indexList.Add(getOpIndex(y - 1, x + 0));
                            indexList.Add(getOpIndex(y + 0, x + 0));
                            indexList.Add(getOpIndex(y + 0, x - 1));
                        }

                    }

                    if (y > 0)
                    {
                        //ref
                        KyonyuPoint pointUp = m_PointList[getOpIndex(y - 1, x)];
                        m_JointList.Add(new KyonyuJoint(point, pointUp));//たて

                        if (y < ROWS - 1)
                        {
                            //ref
                            KyonyuPoint  pointNaname1 = m_PointList[getOpIndex(y - 1, x - 1)];
                            KyonyuJoint jointNaname1 = new KyonyuJoint(point, pointNaname1);
                            jointNaname1.m_Spring *= 0.75f;
                            m_JointList.Add(jointNaname1);//ななめ1（せん断抵抗）

                            //ref
                            KyonyuPoint pointNaname2 = m_PointList[getOpIndex(y - 1, x + 1)];
                            KyonyuJoint jointNaname2 = new KyonyuJoint(point, pointNaname2);
                            jointNaname2.m_Spring *= 0.75f;
                            m_JointList.Add(jointNaname2);//ななめ2（せん断抵抗）
                        }
                    }
                    if (y < ROWS - 1)
                    {
                        //ref
                        KyonyuPoint pointLeft = m_PointList[getOpIndex(y, x - 1)];
                        m_JointList.Add(new KyonyuJoint(point, pointLeft));//よこ
                    }
                    if (y > 1)
                    {
                        //ref
                        KyonyuPoint pointUp2 = m_PointList[getOpIndex(y - 2, x)];
                        KyonyuJoint jointUp2 = new KyonyuJoint(point, pointUp2);
                        if (y < ROWS - 1)
                        {
                            jointUp2.m_Spring *= 1.0f;
                        }
                        else
                        {//極部分
                            jointUp2.m_Spring *= 2.0f;
                            jointUp2.m_Damper *= 2.0f;
                        }
                        m_JointList.Add(jointUp2);//たて　ひとつ飛ばし（角度抵抗）
                    }
                    if (y < ROWS - 1)
                    {
                        //ref
                        KyonyuPoint pointLeft2 = m_PointList[getOpIndex(y, x - 2)];
                        KyonyuJoint jointLeft2 = new KyonyuJoint(point, pointLeft2);
                        jointLeft2.m_Spring *= 1.0f;
                        m_JointList.Add(jointLeft2);//よこ　ひとつ飛ばし（角度抵抗）

                        //            var pointDiagonal:Point = _points[getOpIndex(y,x+_cols/2)];
                        //            var jointDiagonal:Joint = new Joint(point, pointDiagonal);
                        //            jointDiagonal.SPRING *= 0.5;
                        //            _joints.push(jointDiagonal);//よこ　対角線
                    }

                }
                //極部分はバネが集中するので重くする
                //ref
                KyonyuPoint pointPole = m_PointList[m_PointList.Count - 2];
                pointPole.m_Mass *= 1.5f;

                //中心部分を設定
                //ref
                KyonyuPoint pointCenter = m_PointList[m_PointList.Count - 1];
                pointCenter.m_Position = new Vector3(0, 0, RADIUS / 4 * -1);
                pointCenter.m_IsPinned = true;
                for (int volIndex = 0; volIndex < m_PointList.Count - 1; ++volIndex)
                {//圧力
                    KyonyuJoint jointVol = new KyonyuJoint(pointCenter, m_PointList[volIndex]);
                    if (volIndex < m_PointList.Count - 2)
                    {
                        jointVol.m_Spring *= 0.5f;
                    }
                    else
                    {//極部分
                        jointVol.m_Spring *= 2.0f;
                    }
                    m_JointList.Add(jointVol);
                }

                for (int i = 0; i < m_JointList.Count; ++i)
                {
                    //ref
                    KyonyuJoint joint = m_JointList[i];
                    joint.resetNaturalLength();
                }


                //根元を固定します。
                for (int i = 0; i < COLS; ++i)
                {
                    m_PointList[i].m_IsPinned = true;
                    m_PointList[i + COLS].m_IsPinned = true;
                }
            }

            //テクスチャ座標
            for (int i = 0; i < m_PointList.Count; ++i)
            {
                m_PointList[i].m_TexCoord.X = m_PointList[i].m_Position.X / (RADIUS * 2) + 0.5f;
                m_PointList[i].m_TexCoord.Y = m_PointList[i].m_Position.Y / (RADIUS * 2) + 0.5f;
            }
            m_Indices = indexList.ToArray();
            //trace("_poinits.length"+_points.length);
            //trace("_joints.length"+_joints.length);
            //_points.fixed = true;
            //_joints.fixed = true;
            //_faces.fixed = true;
        }
	    private void transfer()
	    {
            if(m_Vertices==null||m_Vertices.Length !=m_PointList.Count)
            {
                m_Vertices=new VertexPositionNormalTexture[m_PointList.Count];
            }
		    for(int i=0;i<m_PointList.Count;++i)
		    {
			    m_Vertices[i].Position=m_PointList[i].m_Position;
			    m_Vertices[i].TextureCoordinate=m_PointList[i].m_TexCoord;
		    }
	    }
        public KyonyuOp()
	    {
		    putPointAndJoint();
	    }
        public void update(float inDt)
        {
            const int times = 20;
            int jointCount=m_JointList.Count;
            int pointCount=m_PointList.Count;
            for (int j = 0; j < times;++j)
            {
                //touch対応
                for(int i=0;i<m_TouchingList.Count;++i)
                {
                    touch(m_TouchingList[i].Item1, m_TouchingList[i].Item2);
                }
                //フェーズを二つに分ける。力更新と位置更新
                // update force
                for(int i=0;i<jointCount;++i) {
                    m_JointList[i].updateForce();
                }
                for(int i=0;i<pointCount;++i) {
                    m_PointList[i].updateForce();
                }
                // update position
                for(int i=0;i<pointCount;++i) {
                    m_PointList[i].updatePosition(inDt/times);
                }
        
            }
    
            //drawLine();
            transfer();
        }
	    public void setPinnedMatrix(Matrix inPinnedMatrix)
        {
            Matrix invCurrentMatrix = new Matrix();
            Matrix.Invert(ref m_PinnedMatrix,out invCurrentMatrix);
            Matrix diff = Matrix.Multiply(invCurrentMatrix, inPinnedMatrix);
            m_PinnedMatrix=inPinnedMatrix;
            if(diff.Translation.Length()>WARP_LENGTH)
            {
                putPointAndJoint();//リセット
                for(int i=0;i<m_PointList.Count;++i)
                {
                    //ref
                    KyonyuPoint p=m_PointList[i];
                    p.m_Position = Vector3.Transform(p.m_Position, m_PinnedMatrix);//今の姿勢へ
                }
            }
            else
            {
		
                for(int i=0;i<m_PointList.Count;++i)
                {
                    //ref
                    KyonyuPoint p = m_PointList[i];
                    if((p.m_IsPinned || p.m_IsDragging))
                    {
                        p.m_Position=Vector3.Transform(p.m_Position,diff);
                    }
                }
            }
        }
        public Tuple<Vector3,Vector3> getBound()
        {
            return new Tuple<Vector3,Vector3>(new Vector3(-RADIUS,-RADIUS,0),new Vector3(+RADIUS,+RADIUS,0));
        }
        public void touch(Vector3 inPosition,float inRadius)
        {
            float closest=1000;//[mm]
            for(int i=0;i<m_PointList.Count;++i)
            {
                //ref
                KyonyuPoint p=m_PointList[i];
                if(!(p.m_IsPinned || p.m_IsDragging))
                {
                    closest=Math.Max(closest,(p.m_Position-inPosition).Length());
                    if((p.m_Position-inPosition).Length()<inRadius)
                    {
                        Vector3 n = (p.m_Position - inPosition);
                        if (n.LengthSquared() != 0)
                        {
                            n.Normalize();
                        }
#if false
                        p.m_Position=n*inRadius+inPosition;
                        p.m_Velocity=new Vector3();//タッチは速度なし
#else
                        Vector3 f=n*(inRadius-(p.m_Position-inPosition).Length())*10.0f;
                        //Vector3 f = n * 300;
                        const float maxForce=250;
                        if(f.Length()>maxForce)
                        {
                            f.Normalize();
                            f *= maxForce;
                        }
                        p.m_Force+=f;
#endif
                        m_IsTouched=true;
                    }
                }
            }
        }
        public void addTouching(Vector3 inPosition,float inRadius)
        {
            m_TouchingList.Add(new Tuple<Vector3,float>(inPosition,inRadius));
        }
        public void clearTouching()
        {
            m_TouchingList.Clear();
            m_IsTouched=false;
        }
        //valid after update
        public bool isTouched()
        {
            return m_IsTouched;
        }
    }
}
