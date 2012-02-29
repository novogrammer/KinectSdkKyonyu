using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KinectSdkKyonyu.Kyonyu
{
    class KyonyuMesh
    {
        protected int[] m_Indices= new int[0];
        protected VertexPositionNormalTexture[] m_Vertices = new VertexPositionNormalTexture[0];
        private VertexBuffer m_VertexBuffer=null;
        private IndexBuffer m_IndexBuffer=null;

        public KyonyuMesh()
        {
        }
        private void setupBuffer(GraphicsDevice inDevice)
        {
            if (m_VertexBuffer == null || m_VertexBuffer.VertexCount != m_Vertices.Length)
            {
                if (m_VertexBuffer!=null)
                {
                    m_VertexBuffer.Dispose();
                }
                m_VertexBuffer = new VertexBuffer(
                    inDevice,
                    typeof(VertexPositionNormalTexture),
                    m_Vertices.Length,
                    BufferUsage.None
                );
            }
            inDevice.SetVertexBuffer(null);
            m_VertexBuffer.SetData<VertexPositionNormalTexture>(m_Vertices);

            if (m_IndexBuffer == null || m_IndexBuffer.IndexCount != m_Indices.Length)
            {
                if (m_IndexBuffer != null)
                {
                    m_IndexBuffer.Dispose();
                }
                m_IndexBuffer = new IndexBuffer(
                    inDevice,
                    IndexElementSize.ThirtyTwoBits,
                    m_Indices.Length,
                    BufferUsage.None
                );
            }
            inDevice.Indices = null;
            m_IndexBuffer.SetData<int>(m_Indices);
        }
        public void prepareDraw(GraphicsDevice inDevice)
        {
            for (int i = 0; i < m_Vertices.Length; ++i)
            {
                m_Vertices[i].Normal = new Vector3();
            }
            for (int i = 0; i < m_Indices.Length / 3; ++i)
            {
                //DirectXは左手系なのでクロスの引数を反転
                Vector3 a = m_Vertices[m_Indices[i * 3 + 1]].Position - m_Vertices[m_Indices[i * 3 + 0]].Position;
                Vector3 b = m_Vertices[m_Indices[i * 3 + 2]].Position - m_Vertices[m_Indices[i * 3 + 0]].Position;
                Vector3 n = Vector3.Cross(b, a);
                if (n.LengthSquared() != 0)
                {
                    n.Normalize();
                }
                for (int j = 0; j < 3; ++j)
                {
                    m_Vertices[m_Indices[i * 3 + j]].Normal += n;
                }
            }

            for (int i = 0; i < m_Vertices.Length; ++i)
            {
                //ゼロ割回避
                if (m_Vertices[i].Normal.LengthSquared() != 0.0)
                {
                    m_Vertices[i].Normal.Normalize();
                }
            }
            setupBuffer(inDevice);
        }
        //描画パスごとに呼び出す
	    public void drawPass(GraphicsDevice inDevice)
	    {
            inDevice.SetVertexBuffer(m_VertexBuffer);
            inDevice.Indices=m_IndexBuffer;
            inDevice.DrawIndexedPrimitives(
                PrimitiveType.TriangleList,
                0,
                0,
                m_Vertices.Length,
                0,
                m_Indices.Length / 3
            );
        }
    }
}
