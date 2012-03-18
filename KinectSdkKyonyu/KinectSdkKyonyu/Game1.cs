#define USE_COLOR_MAP
#define USE_PARALLEL
#define USE_KINECT_THREAD

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Kinect;
using KinectSdkKyonyu.Kyonyu;

namespace KinectSdkKyonyu
{
    using OpTexture = Tuple<Texture2D, Texture2D>;
    using OpTextureMap = Dictionary<int, Tuple<Texture2D, Texture2D>>;
#if USE_KINECT_THREAD
    using System.Threading;
#endif
#if USE_PARALLEL
    using System.Threading.Tasks;
#endif
    /// <summary>
    /// 基底 Game クラスから派生した、ゲームのメイン クラスです。
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        BasicEffect effectNormalTexture;
        BasicEffect effectColor;
        VertexPositionNormalTexture[] vertices = new VertexPositionNormalTexture[]
        {
            new VertexPositionNormalTexture(new Vector3(+200,+100,1000),new Vector3(0,0,1),new Vector2(0,0)),
            new VertexPositionNormalTexture(new Vector3(-200,-100,1000),new Vector3(0,0,1),new Vector2(1,1)),
            new VertexPositionNormalTexture(new Vector3(+200,-100,1000),new Vector3(0,0,1),new Vector2(0,1)),

            new VertexPositionNormalTexture(new Vector3(-200,+100,1000),new Vector3(0,0,1),new Vector2(1,0)),
            new VertexPositionNormalTexture(new Vector3(-200,-100,1000),new Vector3(0,0,1),new Vector2(1,1)),
            new VertexPositionNormalTexture(new Vector3(+200,+100,1000),new Vector3(0,0,1),new Vector2(0,0))
        };
        Texture2D kinectColorTexture;
        VertexBuffer vertexBuffer;

        const int WIDTH = 640;
        const int HEIGHT = 480;

        const int OP_TEXTURE_WIDTH=256;
        const int OP_TEXTURE_HEIGHT=256;
        const float KANSETSU = 150.0f;

        KinectSensor kinectSensor;
        byte[] colorKinectData = new byte[WIDTH * HEIGHT * 4];
        byte[] colorData = new byte[WIDTH * HEIGHT * 4];
        short[] depthData = new short[WIDTH * HEIGHT];
        ColorImagePoint[] mappedDepthData = new ColorImagePoint[WIDTH * HEIGHT];
#if USE_COLOR_MAP
        ColorImagePoint[] mappedColorData = new ColorImagePoint[WIDTH * HEIGHT];
#endif
        Skeleton[] skeletonData = new Skeleton[0];

        //OpenNIで取得できた値
        static readonly int ZPD=120;
        static readonly float ZPPS=0.105200f;
        //カメラの画角
    	float m_Fovy=MathHelper.ToDegrees((float)Math.Atan((ZPPS*HEIGHT/2*2)/ZPD))*2;
        //1ピクセルを現実の距離にするための係数
    	float m_PixelToXY=ZPPS/ZPD*2;//二倍する必要あり？
        float m_RotY=0;
        RenderTarget2D m_OpRenderTarget;
        BasicEffect m_OpRenderEffect;

        const int MAX_NUMBER_USERS = 7;//最大7人？
        KyonyuPairOp[] m_OpList = new KyonyuPairOp[MAX_NUMBER_USERS]
        {
            new KyonyuPairOp(),new KyonyuPairOp(),
            new KyonyuPairOp(),new KyonyuPairOp(),
            new KyonyuPairOp(),new KyonyuPairOp(),
            new KyonyuPairOp()
        };

        //重たいときはここで調整
        const int stepCloud = 2;//1,2,4,8
        VertexPositionColor[] vertexCloud = new VertexPositionColor[(HEIGHT * WIDTH / (stepCloud * stepCloud)) * 6];
        VertexBuffer vertexBufferCloud;

        OpTextureMap m_OpTextureMap = new OpTextureMap();

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = WIDTH;
            graphics.PreferredBackBufferHeight = HEIGHT;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 30.0);
            Content.RootDirectory = "Content";
        }
        void setupPerspective()
        {
            //cameraFovy[deg] 100[mm]to10000[mm]
            effectColor.Projection=effectNormalTexture.Projection = Matrix.CreatePerspectiveFieldOfView
            (
            MathHelper.ToRadians(m_Fovy),
            GraphicsDevice.Viewport.AspectRatio,
            100,
            10000
            );
        }
        void setupModelView()
        {

	        Vector3 eyeOrg=new Vector3(0,0,0);
	        Vector3 center=new Vector3(0,0,1500);//1.5[m]
	        Vector3 up=new Vector3(0,1,0);
	        Matrix rot=Matrix.CreateRotationY(MathHelper.ToRadians(m_RotY));
            Vector3 eye =  Vector3.Transform((eyeOrg - center), rot) + center;
            //DirectXはModelとViewが分かれている
            effectColor.View = effectNormalTexture.View = Matrix.CreateLookAt
            (
                eye,
                center,
                up
            );
        }
        void setupCloudVertex()
        {
        }

        Color getColorAt(int inX,int inY)
        {
            const int bytesPerPixel=4;
            const int bytesPerLine=WIDTH*bytesPerPixel;
#if USE_COLOR_MAP
            int ofs = bytesPerLine * inY + bytesPerPixel * inX;
#else
            ColorImagePoint imagePoint = mappedDepthData[WIDTH * inY + inX];
            imagePoint.X = (int)MathHelper.Clamp(imagePoint.X, 0, WIDTH - 1);
            imagePoint.Y = (int)MathHelper.Clamp(imagePoint.Y, 0, HEIGHT - 1);
            int ofs = bytesPerLine * imagePoint.Y + bytesPerPixel * imagePoint.X;
#endif
            Color color = new Color(colorData[ofs + 0], colorData[ofs + 1], colorData[ofs + 2]);
            return color;
        }
        Vector3 getWorldCoordinateAt(int inX, int inY)
        {
            const int line = WIDTH;
#if USE_COLOR_MAP
            ColorImagePoint imagePoint = mappedColorData[WIDTH * inY + inX];
            short z = (short)(depthData[line * imagePoint.Y + imagePoint.X] / 8);
#else
            short z =(short)( depthData[line * inY + inX]/8);
#endif
            if (z < 800 || z > 4000)
            {
                z = 4000;//4[m]まで
            }
            Vector3 coord = new Vector3((inX - WIDTH / 2) * m_PixelToXY * z, -(inY - HEIGHT / 2) * m_PixelToXY * z, z);
            return coord;
        }
        void drawPointCloud()
        {
#if USE_PARALLEL
            Parallel.For(0, (HEIGHT / stepCloud) * (WIDTH / stepCloud), i =>
            {
                int x = i % (WIDTH / stepCloud);
                int y = i / (WIDTH / stepCloud);
                Vector3 pos = getWorldCoordinateAt(x * stepCloud, y * stepCloud);
                pos.X *= -1;//ミラー表示する
                Color col = getColorAt(x * stepCloud, y * stepCloud);

                const float s = stepCloud * 0.5f;
                float sz = 1 * m_PixelToXY * pos.Z * s;

                Vector3 p1 = pos + new Vector3(-sz, -sz, 0);
                Vector3 p2 = pos + new Vector3(+sz, -sz, 0);
                Vector3 p3 = pos + new Vector3(+sz, +sz, 0);
                Vector3 p4 = pos + new Vector3(-sz, +sz, 0);

                //DirectXなので頂点の順番を変える
                vertexCloud[i * 6 + 0] = new VertexPositionColor(p1, col);
                vertexCloud[i * 6 + 1] = new VertexPositionColor(p2, col);
                vertexCloud[i * 6 + 2] = new VertexPositionColor(p3, col);
                vertexCloud[i * 6 + 3] = new VertexPositionColor(p3, col);
                vertexCloud[i * 6 + 4] = new VertexPositionColor(p4, col);
                vertexCloud[i * 6 + 5] = new VertexPositionColor(p1, col);
                
            });
            
#else
            for (int y = 0; y < HEIGHT; y += stepCloud)
            {
                for (int x = 0; x < WIDTH; x += stepCloud)
                {
                    Vector3 pos=getWorldCoordinateAt(x,y);
                    pos.X *= -1;//ミラー表示する
                    Color col=getColorAt(x,y);

                    const float s = stepCloud * 0.5f;
                    float sz = 1 * m_PixelToXY * pos.Z * s;

                    Vector3 p1 = pos + new Vector3(-sz, -sz, 0);
                    Vector3 p2 = pos + new Vector3(+sz, -sz, 0);
                    Vector3 p3 = pos + new Vector3(+sz, +sz, 0);
                    Vector3 p4 = pos + new Vector3(-sz, +sz, 0);

                    int i = y * (WIDTH / stepCloud / stepCloud) + x / stepCloud;
                    //DirectXなので頂点の順番を変える
                    vertexCloud[i * 6 + 0] = new VertexPositionColor(p1, col);
                    vertexCloud[i * 6 + 1] = new VertexPositionColor(p2, col);
                    vertexCloud[i * 6 + 2] = new VertexPositionColor(p3, col);
                    vertexCloud[i * 6 + 3] = new VertexPositionColor(p3, col);
                    vertexCloud[i * 6 + 4] = new VertexPositionColor(p4, col);
                    vertexCloud[i * 6 + 5] = new VertexPositionColor(p1, col);
                }
	        }
#endif
            GraphicsDevice.SetVertexBuffer(null);
            vertexBufferCloud.SetData(vertexCloud);
            GraphicsDevice.SetVertexBuffer(vertexBufferCloud);

            GraphicsDevice.DrawPrimitives(
                PrimitiveType.TriangleList,
                0,
                vertexCloud.Length / 3
            );
        }


        /// <summary>
        /// ゲームが実行を開始する前に必要な初期化を行います。
        /// ここで、必要なサービスを照会して、関連するグラフィック以外のコンテンツを
        /// 読み込むことができます。base.Initialize を呼び出すと、使用するすべての
        /// コンポーネントが列挙されるとともに、初期化されます。
        /// </summary>
        protected override void Initialize()
        {
            if (KinectSensor.KinectSensors.Count == 0)
            {
                throw new Exception("Kinectが接続されていません。接続してください。");
            }
            setupCloudVertex();
            kinectSensor = KinectSensor.KinectSensors[0];

#if USE_KINECT_THREAD
            Thread kinectThread = new Thread(() => 
            {
                kinectSensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(kinectAllFramesReady);
            });
            kinectThread.Start();
            kinectThread.Join();
#endif
            kinectSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            kinectSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            //積極的にノイズにより揺らす
            TransformSmoothParameters smooth = new TransformSmoothParameters()
            {
                Correction=1.0f,
                JitterRadius=0.0f,
                Smoothing=0.0f
            };
            kinectSensor.SkeletonStream.Enable(smooth);
            kinectSensor.Start();
            base.Initialize();
        }

        /// <summary>
        /// LoadContent はゲームごとに 1 回呼び出され、ここですべてのコンテンツを
        /// 読み込みます。
        /// </summary>
        protected override void LoadContent()
        {
            // 新規の SpriteBatch を作成します。これはテクスチャーの描画に使用できます。
            spriteBatch = new SpriteBatch(GraphicsDevice);

            effectNormalTexture = new BasicEffect(GraphicsDevice)
            {
                TextureEnabled = true,
                SpecularColor=new Vector3(0.0f,0.0f,0.0f),
                DiffuseColor=new Vector3(1.0f,1.0f,1.0f),
                AmbientLightColor = new Vector3(2.0f, 2.0f, 2.0f)
            };
            effectNormalTexture.EnableDefaultLighting();
            effectNormalTexture.DirectionalLight0.Enabled = true;
            effectNormalTexture.DirectionalLight1.Enabled = false;
            effectNormalTexture.DirectionalLight2.Enabled = false;
            DirectionalLight light = effectNormalTexture.DirectionalLight0;
            light.Direction = new Vector3(0.0f, -0.2f, 1.0f);

            effectColor = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true
            };
            kinectColorTexture =new Texture2D(GraphicsDevice, WIDTH, HEIGHT);
            
            vertexBuffer = new VertexBuffer(
                GraphicsDevice,
                typeof(VertexPositionNormalTexture),
                vertices.Length,
                BufferUsage.None
                );
            vertexBuffer.SetData(vertices);
            vertexBufferCloud = new VertexBuffer(
                GraphicsDevice,
                typeof(VertexPositionColor),
                vertexCloud.Length,
                BufferUsage.None
                );
            m_OpRenderTarget = new RenderTarget2D(GraphicsDevice, OP_TEXTURE_WIDTH, OP_TEXTURE_HEIGHT);
            m_OpRenderEffect = new BasicEffect(GraphicsDevice)
            {
                TextureEnabled=true
            };
        }

        /// <summary>
        /// UnloadContent はゲームごとに 1 回呼び出され、ここですべてのコンテンツを
        /// アンロードします。
        /// </summary>
        protected override void UnloadContent()
        {
            spriteBatch.Dispose();
            effectNormalTexture.Dispose();
            kinectColorTexture.Dispose();
            vertexBuffer.Dispose();
            vertexBufferCloud.Dispose();
            if (kinectSensor != null)
            {
                kinectSensor.Dispose();
            }
            m_OpRenderTarget.Dispose();
            m_OpRenderEffect.Dispose();
        }
        void updateAllFrames(ColorImageFrame inColor,DepthImageFrame inDepth,SkeletonFrame inSkelton)
        {
            if(inColor!=null)
            {
                inColor.CopyPixelDataTo(this.colorKinectData);
                for (int i = 0; i < this.colorData.Length/4;++i)
                {
                    this.colorData[i * 4 + 0] = this.colorKinectData[i * 4 + 2];
                    this.colorData[i * 4 + 1] = this.colorKinectData[i * 4 + 1];
                    this.colorData[i * 4 + 2] = this.colorKinectData[i * 4 + 0];
                    this.colorData[i * 4 + 3] = 255;
                }

                if (inDepth != null)
                {
                    inDepth.CopyPixelDataTo(this.depthData);
                    kinectSensor.MapDepthFrameToColorFrame(inDepth.Format, depthData, inColor.Format, mappedDepthData);
#if USE_COLOR_MAP
#if USE_PARALLEL
                    Parallel.For(0, mappedDepthData.Length, i =>
                    {
                        ColorImagePoint imagePoint = mappedDepthData[i];
                        imagePoint.X = (int)MathHelper.Clamp(imagePoint.X, 0, WIDTH - 1);
                        imagePoint.Y = (int)MathHelper.Clamp(imagePoint.Y, 0, HEIGHT - 1);
                        mappedColorData[imagePoint.Y * WIDTH + imagePoint.X].X = i % WIDTH;
                        mappedColorData[imagePoint.Y * WIDTH + imagePoint.X].Y = i / WIDTH;
                    });
#else
                    for(int i=0;i<mappedDepthData.Length;++i)
                    {
                        ColorImagePoint imagePoint = mappedDepthData[i];
                        imagePoint.X = (int)MathHelper.Clamp(imagePoint.X, 0, WIDTH - 1);
                        imagePoint.Y = (int)MathHelper.Clamp(imagePoint.Y, 0, HEIGHT - 1);
                        mappedColorData[imagePoint.Y * WIDTH + imagePoint.X].X=i%WIDTH;
                        mappedColorData[imagePoint.Y * WIDTH + imagePoint.X].Y=i/WIDTH;
                    }
#endif
#endif
                }
                if (inSkelton != null)
                {
                    if (this.skeletonData == null || this.skeletonData.Length != inSkelton.SkeletonArrayLength)
                    {
                        this.skeletonData = new Skeleton[inSkelton.SkeletonArrayLength];
                    }
                    inSkelton.CopySkeletonDataTo(this.skeletonData);
                }
            }
        }
        void kinectAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (ColorImageFrame color = e.OpenColorImageFrame())
            {
                using (DepthImageFrame depth = e.OpenDepthImageFrame())
                {
                    using (SkeletonFrame skelton = e.OpenSkeletonFrame())
                    {
                        updateAllFrames(color, depth, skelton);
                    }
                }
            }

        }
        //SkeltonPoint[m] -> Vector3[mm]
        Vector3 toVector3(SkeletonPoint inSkeltonPoint)
        {
            return new Vector3(
                inSkeltonPoint.X * 1000,
                inSkeltonPoint.Y * 1000,
                inSkeltonPoint.Z * 1000);
        }
        Matrix getOpMatrix(Skeleton inSkelton)
        {
            Vector3 s = toVector3(inSkelton.Joints[JointType.Spine].Position);
            Vector3 sc = toVector3(inSkelton.Joints[JointType.ShoulderCenter].Position);
            Vector3 sl = toVector3(inSkelton.Joints[JointType.ShoulderLeft].Position);
            Vector3 sr = toVector3(inSkelton.Joints[JointType.ShoulderRight].Position);
            Vector3 up = (sc - s);
            Vector3 right = (sr - sl);
            Vector3 forward = Vector3.Cross(right,up);//直交しているという前提
            if (
                (right.LengthSquared() == 0) ||
                (up.LengthSquared() == 0) ||
                (forward.LengthSquared() == 0))
            {
                return Matrix.Identity;
            }
            else
            {
                right.Normalize();
                up.Normalize();
                forward.Normalize();
                Matrix m = Matrix.Identity;
                m.Forward = forward;
                m.Up = up;
                m.Right = right;
                m.Translation = sc;
                return m;
            }
        }
        Texture2D capture(Tuple<Vector3, Vector3> inBound,Matrix inMatrix)
        {
            RenderTarget2D renderTarget = new RenderTarget2D(GraphicsDevice, OP_TEXTURE_WIDTH, OP_TEXTURE_HEIGHT);
            GraphicsDevice.SetRenderTarget(renderTarget);
            GraphicsDevice.Clear(Color.Gray);

            Vector2 p1;
            Vector2 p2;
            Vector3 to = inMatrix.Translation;
            {
                Vector3 p = inBound.Item1;
                p.X *= -1;
                p += to;
                p1 = new Vector2(p.X / (m_PixelToXY * p.Z) + WIDTH / 2, p.Y / (m_PixelToXY * p.Z) + HEIGHT / 2);
                p1.X = WIDTH-p1.X;
                p1.Y = HEIGHT-p1.Y;
            }
            {
                Vector3 p = inBound.Item2;
                p.X *= -1;
                p += to;
                p2 = new Vector2(p.X / (m_PixelToXY * p.Z) + WIDTH / 2, p.Y / (m_PixelToXY * p.Z) + HEIGHT / 2);
                p2.X = WIDTH-p2.X;
                p2.Y = HEIGHT-p2.Y;
            }
            spriteBatch.Begin();
            spriteBatch.Draw(
                kinectColorTexture,
                new Rectangle(0, 0, OP_TEXTURE_WIDTH, OP_TEXTURE_HEIGHT),
                new Rectangle((int)p1.X,(int)(p1.Y),(int)(p2.X-p1.X),(int)(p2.Y-p1.Y)),
                Color.White);
            spriteBatch.End();

            GraphicsDevice.SetRenderTarget(null);
            return renderTarget;
        }
        /// <summary>
        /// ワールドの更新、衝突判定、入力値の取得、オーディオの再生などの
        /// ゲーム ロジックを、実行します。
        /// </summary>
        /// <param name="gameTime">ゲームの瞬間的なタイミング情報</param>
        protected override void Update(GameTime gameTime)
        {
            if (
                (Keyboard.GetState().IsKeyDown(Keys.Escape))
            )
            {
                this.Exit();
            }
            
            if(
                (Keyboard.GetState().IsKeyDown(Keys.Space))||
                (Mouse.GetState().LeftButton == ButtonState.Pressed)
            )
            {
                graphics.ToggleFullScreen();
            }
#if !USE_KINECT_THREAD
            using (ColorImageFrame color = kinectSensor.ColorStream.OpenNextFrame(0))
            {
                using (DepthImageFrame depth = kinectSensor.DepthStream.OpenNextFrame(0))
                {
                    using (SkeletonFrame skelton = kinectSensor.SkeletonStream.OpenNextFrame(0))
                    {
                        updateAllFrames(
                            color,
                            depth,
                            skelton
                        );
                    }
                }
            }
#endif
            //GC.Collect();
            for (int i = 0; i < m_OpList.Length; ++i)
            {
                m_OpList[i].clearTouching();
            }
            for (int i = 0; i < skeletonData.Length; ++i)
            {
                if (skeletonData[i]!=null && skeletonData[i].TrackingState == SkeletonTrackingState.Tracked)
                {
                    Matrix mirrorMatrix = Matrix.CreateScale(new Vector3(-1, 1, 1));
                    Vector3[] touchList = new Vector3[4]
                    {
                        Vector3.Transform(toVector3(skeletonData[i].Joints[JointType.ElbowRight].Position),mirrorMatrix),
                        Vector3.Transform(toVector3(skeletonData[i].Joints[JointType.ElbowLeft].Position),mirrorMatrix),
                        Vector3.Transform(toVector3(skeletonData[i].Joints[JointType.HandRight].Position),mirrorMatrix),
                        Vector3.Transform(toVector3(skeletonData[i].Joints[JointType.HandLeft].Position),mirrorMatrix)
                    };
                    foreach (Vector3 pos in touchList)
                    {
                        for (int j = 0; j < MAX_NUMBER_USERS; ++j)
                        {
                            //他人のもタッチ
                            m_OpList[j].addTouching(pos, KANSETSU);
                        }
                    }
                }
            }

            for (int i = 0; i < skeletonData.Length; ++i)
            {
                if (skeletonData[i]!=null && skeletonData[i].TrackingState == SkeletonTrackingState.Tracked)
                {
                    Matrix opMatrix = getOpMatrix(skeletonData[i]) * Matrix.CreateScale(new Vector3(-1, 1, 1));

                    m_OpList[i].setPinnedMatrix(opMatrix);
                    //Console.WriteLine(opMatrix.ToString());
                    m_OpList[i].update(1.0f / 30);
                    if (m_OpList[i].isTouched())
                    {
                        //音を鳴らすとか・・
                    }
                    if (!m_OpTextureMap.ContainsKey(skeletonData[i].TrackingId))
                    {
                        //撮影する
                        Texture2D t1 = capture(m_OpList[i].getBound(0), opMatrix);
                        Texture2D t2 = capture(m_OpList[i].getBound(1), opMatrix);
                        m_OpTextureMap[skeletonData[i].TrackingId] = new OpTexture(t1, t2);
                        m_OpList[i].setTexture(m_OpTextureMap[skeletonData[i].TrackingId]);
                    }
                }
            }

#if DEBUG
            //DateTime d = DateTime.UtcNow;
            //if (previousUpdate != null)
            //{
            //    Console.WriteLine("update" + (d - previousUpdate).TotalMilliseconds.ToString());
            //}
            //previousUpdate = d;
#endif
            base.Update(gameTime);
        }
#if DEBUG
        DateTime previousUpdate;
#endif

        /// <summary>
        /// ゲームが自身を描画するためのメソッドです。
        /// </summary>
        /// <param name="gameTime">ゲームの瞬間的なタイミング情報</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            setupPerspective();
            setupModelView();

            GraphicsDevice.Textures[0] = null;
            kinectColorTexture.SetData(this.colorData);

            foreach (var pass in effectColor.CurrentTechnique.Passes)
            {
                pass.Apply();
                drawPointCloud();
            }
            effectNormalTexture.Texture = kinectColorTexture;
            for (int i = 0; i < skeletonData.Length; ++i)
            {
                if (skeletonData[i]!=null && skeletonData[i].TrackingState == SkeletonTrackingState.Tracked)
                {
                    m_OpList[i].draw(GraphicsDevice, effectNormalTexture);
                }
            }
#if DEBUG
            //DateTime d = DateTime.UtcNow;
            //if (previousDraw != null)
            //{
            //    Console.WriteLine("draw" + (d - previousDraw).TotalMilliseconds.ToString());
            //}
            //previousDraw = d;
#endif
            base.Draw(gameTime);
        }
#if DEBUG
        DateTime previousDraw;
#endif
    }
}
