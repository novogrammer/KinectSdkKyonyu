using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KinectSdkKyonyu.Kyonyu
{
    class KyonyuPairOp
    {
        const int OP_QTY = 2;
        KyonyuOp[] m_OpList = new KyonyuOp[OP_QTY] { new KyonyuOp(), new KyonyuOp() };
        Vector3 m_OpOffsetSide = new Vector3(75, 0, 0);
        Vector3 m_OpOffsetFront = new Vector3(0, -250, 70);
        //一致しているため、回転なし
        Matrix m_OpRot=Matrix.Identity;
        Tuple<Texture2D,Texture2D> m_Texture;//cache
        public KyonyuPairOp()
        {
        }
        public Tuple<Vector3, Vector3> getBound(int inIndex)
        {
            Tuple<Vector3,Vector3> bound=m_OpList[inIndex].getBound();
            return new Tuple<Vector3,Vector3>(bound.Item1+getOffset(inIndex),bound.Item2+getOffset(inIndex));
        }
        public void update(float inDt)
        {
            m_OpList[0].update(inDt);
            m_OpList[1].update(inDt);
        }
        public void setPinnedMatrix(Matrix inPinnedMatrix)
        {
            m_OpList[0].setPinnedMatrix(inPinnedMatrix * m_OpRot * Matrix.CreateTranslation(getOffset(0)));
            m_OpList[1].setPinnedMatrix(inPinnedMatrix * m_OpRot * Matrix.CreateTranslation(getOffset(1)));

            m_OpList[0].setPinnedMatrix(Matrix.CreateTranslation(getOffset(0)) * m_OpRot * inPinnedMatrix);
            m_OpList[1].setPinnedMatrix(Matrix.CreateTranslation(getOffset(1)) * m_OpRot * inPinnedMatrix);

        }
        public void draw(GraphicsDevice inDevice,BasicEffect inEffect)
        {
            for (int i = 0; i < 2; ++i)
            {
                m_OpList[i].prepareDraw(inDevice);
                inEffect.Texture = (m_Texture == null) ? (null) : ((i == 0) ? m_Texture.Item1 : m_Texture.Item2);
                foreach (var pass in inEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    m_OpList[i].drawPass(inDevice);
                }
            }
        }
        public void setTexture(Tuple<Texture2D, Texture2D> inTexture)
        {
            m_Texture=inTexture;
        }
        public void addTouching(Vector3 inPosition, float inRadius)
        {
            m_OpList[0].addTouching(inPosition, inRadius);
            m_OpList[1].addTouching(inPosition, inRadius);
        }
        public void clearTouching()
        {
            m_OpList[0].clearTouching();
            m_OpList[1].clearTouching();
        }
    
        //valid after update
        public bool isTouched()
        {
            return m_OpList[0].isTouched() || m_OpList[1].isTouched();
        }

        private Vector3 getOffset(int inIndex)
        {
            if (inIndex == 0)
            {
                return m_OpOffsetFront + m_OpOffsetSide * -1;
            }
            else
            {
                return m_OpOffsetFront + m_OpOffsetSide * +1;
            }
        }
    }
}
