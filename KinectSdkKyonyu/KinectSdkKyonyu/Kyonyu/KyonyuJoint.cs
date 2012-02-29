using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace KinectSdkKyonyu.Kyonyu
{
    class KyonyuJoint
    {
        public KyonyuPoint m_Point;
        public KyonyuPoint m_Target;
        public float m_Spring=10.0f;//[N/mm]
        public float m_Damper=0.01f;//[N/(mm/s)]
        public float m_NaturalLength=0.0f;
        public KyonyuJoint(KyonyuPoint inPoint, KyonyuPoint inTarget)
        {
            m_Point = inPoint;
            m_Target = inTarget;
            resetNaturalLength();
        }
        public void resetNaturalLength()
        {
            if (!(m_Point!=null && m_Target!=null))
            {
                return;
            }
            m_NaturalLength = (m_Point.m_Position-m_Target.m_Position).Length();
            //trace( _point.name+ " " +_target.name +" length:"+ _naturalLength);
        }
        public void updateForce()
        {
            if (!(m_Point != null && m_Target != null))
            {
                return;
            }
            //バネの力
            Vector3 dx = (m_Target.m_Position) - (m_Point.m_Position);
            //単位ベクトル
            Vector3 nx = new Vector3(dx.X, dx.Y, dx.Z);
            if (nx.LengthSquared() != 0)
            {
                nx.Normalize();
            }

            Vector3 springForce = nx * ((dx.Length() - m_NaturalLength) * m_Spring);

            //ダンパの力
            Vector3 dv = (m_Target.m_Velocity) - (m_Point.m_Velocity);
            Vector3 damperForce = dv * m_Damper;

            //合力
            Vector3 totalForce = springForce + damperForce;
            m_Point.m_Force += totalForce;
            //逆の力をかける
            m_Target.m_Force += totalForce * -1;

        }
        
    }
}
