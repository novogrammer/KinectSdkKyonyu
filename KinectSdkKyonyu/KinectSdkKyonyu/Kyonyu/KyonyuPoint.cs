using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace KinectSdkKyonyu.Kyonyu
{
    class KyonyuPoint
    {
        static readonly float GRAVITY_A = 9.8f * 1000;//[mm/(s*s)]
        //
//        static readonly float GRAVITY_A = 98.0f * 1000;//[mm/(s*s)]
        static readonly float AIR_FRICTION = 0.005f;//[N/(mm/s)]
        public Vector3 m_Position=new Vector3();//[mm]
        public Vector3 m_Velocity = new Vector3();//[mm/s]
        public Vector3 m_Force = new Vector3();//[mm/(s*s)]
        public Vector2 m_TexCoord = new Vector2();//uv
        public float m_Mass = 6.0f / 1000;//[kg];
        public bool m_IsPinned = false;
        public bool m_IsDragging = false;
        public void updateForce()
        {
            m_Force += m_Velocity * (AIR_FRICTION * -1);
        }

        public void updatePosition(float inDt)
        {
            if (m_IsDragging || m_IsPinned)
            {
                m_Velocity=new Vector3();
            }
            else
            {
                Vector3 a = m_Force/m_Mass;
                a.Y -= GRAVITY_A;
#if false
                m_Velocity+=a*inDt;
                m_Position+=m_Velocity*inDt;
#else
                Vector3 k=new Vector3();
                Vector3 l=new Vector3();
                rungeKutta(a, m_Velocity, m_Position,inDt,out k,out l);
                m_Position+=k;
                m_Velocity+=l;
#endif
            }
            m_Force = new Vector3();
        }

        //オイラー法で暴走したので、ルンゲクッタ法という積分法を使う
        //http://www6.ocn.ne.jp/~simuphys/runge-kutta.html
        private void rungeKutta(Vector3 inA, Vector3 inV, Vector3 inX, float inDt, out Vector3 outK, out Vector3 outL)
        {
            Vector3 x1 = inV*inDt;
            Vector3 v1 = inA*inDt;
            Vector3 x2 = (inV+(v1*0.5f) )*inDt;
            Vector3 v2 = inA*(inDt * 0.5f);//あってる？　Aを求めなおす必要がある？
            Vector3 x3 = (inV+v2*0.5f)*inDt;
            Vector3 v3 = inA*(inDt * 0.5f);//あってる？　Aを求めなおす必要がある？
            Vector3 x4 = (inV+v3)*inDt;
            Vector3 v4 = inA*inDt;//あってる？　Aを求めなおす必要がある？
            outK=(x1+(x2*2)+(x3*2)+x4)/6;
            outL=(v1+(v2*2)+(v3*2)+v4)/6;
        }
    }
}
