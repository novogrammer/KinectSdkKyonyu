#define USE_COLOR_MAP
//#define USE_PARALLEL
//#define USE_READY_EVENT
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
        //2枚のテクスチャを交互に利用する
        Texture2D[] kinectColorTextures;
        int renderingTexture = 0;
        VertexBuffer vertexBuffer;

        KinectSensor kinectSensor;
        byte[] colorData=new byte[640*480*4];
        short[] depthData=new short[640*480];
        ColorImagePoint[] mappedDepthData = new ColorImagePoint[640 * 480];
#if USE_COLOR_MAP
        ColorImagePoint[] mappedColorData = new ColorImagePoint[640 * 480];
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

        const int MAX_NUMBER_USERS = 7;//3bit使い、0は無効なため
        KyonyuPairOp[] m_OpList = new KyonyuPairOp[MAX_NUMBER_USERS]
        {
            new KyonyuPairOp(),new KyonyuPairOp(),
            new KyonyuPairOp(),new KyonyuPairOp(),
            new KyonyuPairOp(),new KyonyuPairOp(),
            new KyonyuPairOp()
        };

        const int WIDTH = 640;
        const int HEIGHT = 480;

        //重たいときはここで調整
        const int stepCloud = 8;//1,2,4,8
        VertexPositionColor[] vertexCloud = new VertexPositionColor[(HEIGHT * WIDTH / (stepCloud * stepCloud)) * 6];
        VertexBuffer vertexBufferCloud;


        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = WIDTH;
            graphics.PreferredBackBufferHeight = HEIGHT;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 30.0);
            Content.RootDirectory = "Content";
        }
        OpTextureMap m_OpTextureMap=new OpTextureMap();

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
            imagePoint.X =Math.Max(Math.Min(imagePoint.X, WIDTH-1), 0);
            imagePoint.Y =Math.Max(Math.Min(imagePoint.Y, HEIGHT-1), 0);
            int ofs = bytesPerLine * imagePoint.Y + bytesPerPixel * imagePoint.X;
#endif
            Color color = new Color(colorData[ofs + 0], colorData[ofs + 1], colorData[ofs + 2]);
            return color;
        }
        Vector3 getWorldCoordinateAt(int inX, int inY)
        {
            const int line = 640;
#if USE_COLOR_MAP
            ColorImagePoint imagePoint = mappedColorData[WIDTH * inY + inX];
            short z = (short)(depthData[line * imagePoint.Y + imagePoint.X] / 8);
#else
            short z =(short)( depthData[line * inY + inX]/8);
#endif
            if ( z < 800)
            {
                z = 800;//0.8[m]から
            }else if (z > 4000)
            {
                z = 4000;//4[m]まで
            }
            Vector3 coord = new Vector3(-(inX - WIDTH / 2) * m_PixelToXY * z, -(inY - HEIGHT / 2) * m_PixelToXY * z, z);
            return coord;
        }
        void drawPointCloud()
        {
#if USE_PARALLEL
            Parallel.For(0, HEIGHT / stepCloud, y =>
            {
                Parallel.For(0, WIDTH / stepCloud, x =>
                {
                    Vector3 pos = getWorldCoordinateAt(x * stepCloud, y * stepCloud);
                    Color col = getColorAt(x * stepCloud, y * stepCloud);

                    const float s = stepCloud * 0.5f;
                    float sz = 1 * m_PixelToXY * pos.Z * s;

                    Vector3 p1 = pos + new Vector3(-sz, -sz, 0);
                    Vector3 p2 = pos + new Vector3(+sz, -sz, 0);
                    Vector3 p3 = pos + new Vector3(+sz, +sz, 0);
                    Vector3 p4 = pos + new Vector3(-sz, +sz, 0);

                    int i = y * WIDTH/stepCloud  + x;
                    //DirectXなので頂点の順番を変える
                    vertexCloud[i * 6 + 0] = new VertexPositionColor(p1, col);
                    vertexCloud[i * 6 + 1] = new VertexPositionColor(p2, col);
                    vertexCloud[i * 6 + 2] = new VertexPositionColor(p3, col);
                    vertexCloud[i * 6 + 3] = new VertexPositionColor(p3, col);
                    vertexCloud[i * 6 + 4] = new VertexPositionColor(p4, col);
                    vertexCloud[i * 6 + 5] = new VertexPositionColor(p1, col);

                });
            });
#else
            for (int y = 0; y < HEIGHT; y += stepCloud)
            {
                for (int x = 0; x < WIDTH; x += stepCloud)
                {
                    Vector3 pos=getWorldCoordinateAt(x,y);
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
#if USE_READY_EVENT
            kinectSensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(kinectAllFramesReady);
#endif
            kinectSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            kinectSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            kinectSensor.SkeletonStream.Enable();
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
                TextureEnabled = true
            };
            effectNormalTexture.EnableDefaultLighting();
            effectColor = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true
            };
            kinectColorTextures =new Texture2D[2]{ new Texture2D(GraphicsDevice, WIDTH, HEIGHT),new Texture2D(GraphicsDevice, WIDTH, HEIGHT)};
            
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
        }

        /// <summary>
        /// UnloadContent はゲームごとに 1 回呼び出され、ここですべてのコンテンツを
        /// アンロードします。
        /// </summary>
        protected override void UnloadContent()
        {
            effectNormalTexture.Dispose();
            vertexBuffer.Dispose();
            vertexBufferCloud.Dispose();
        }
        void updateAllFrames(ColorImageFrame inColor,DepthImageFrame inDepth,SkeletonFrame inSkelton)
        {
            if(inColor!=null)
            {
                inColor.CopyPixelDataTo(this.colorData);
                for (int i = 0; i < this.colorData.Length/4;++i)
                {
                    byte temp=this.colorData[i * 4 + 0];
                    this.colorData[i * 4 + 0] = this.colorData[i * 4 + 2];
                    this.colorData[i * 4 + 2] = temp;
                    this.colorData[i * 4 + 3] = 255;
                }
                kinectColorTextures[1 - renderingTexture].SetData(this.colorData);

                if (inDepth != null)
                {
                    inDepth.CopyPixelDataTo(this.depthData);
                    kinectSensor.MapDepthFrameToColorFrame(inDepth.Format, depthData, inColor.Format, mappedDepthData);
#if USE_COLOR_MAP
#if USE_PARALLEL
                    Parallel.For(0, mappedDepthData.Length, i =>
                    {
                        ColorImagePoint imagePoint = mappedDepthData[i];
                        imagePoint.X = Math.Max(Math.Min(imagePoint.X, WIDTH - 1), 0);
                        imagePoint.Y = Math.Max(Math.Min(imagePoint.Y, HEIGHT - 1), 0);
                        mappedColorData[imagePoint.Y * WIDTH + imagePoint.X].X = i % WIDTH;
                        mappedColorData[imagePoint.Y * WIDTH + imagePoint.X].Y = i / WIDTH;
                    });
#else
                    for(int i=0;i<mappedDepthData.Length;++i)
                    {
                        ColorImagePoint imagePoint = mappedDepthData[i];
                        imagePoint.X =Math.Max(Math.Min(imagePoint.X, WIDTH-1), 0);
                        imagePoint.Y =Math.Max(Math.Min(imagePoint.Y, HEIGHT-1), 0);
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
        /// <summary>
        /// ワールドの更新、衝突判定、入力値の取得、オーディオの再生などの
        /// ゲーム ロジックを、実行します。
        /// </summary>
        /// <param name="gameTime">ゲームの瞬間的なタイミング情報</param>
        protected override void Update(GameTime gameTime)
        {
            // ゲームの終了条件をチェックします。
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();
#if !USE_READY_EVENT
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
                if(skeletonData[i].TrackingState==SkeletonTrackingState.Tracked)
                {
                    for (int j = 0; j < MAX_NUMBER_USERS; ++j)
                    {
                        //他人のもタッチ
                        //m_OpList[j].addTouching(pos, KANSETSU);
                    }
                }
            }

            for (int i = 0; i < skeletonData.Length; ++i)
            {
                if (skeletonData[i].TrackingState == SkeletonTrackingState.Tracked)
                {
                    SkeletonPoint point=skeletonData[i].Joints[JointType.ShoulderCenter].Position;

                    Matrix m = Matrix.CreateTranslation(new Vector3(point.X*-1000, point.Y*1000, point.Z*1000));
                    m_OpList[i].setPinnedMatrix(m);
                    m_OpList[i].update(1.0f / 30);
                    if (m_OpList[i].isTouched())
                    {
                        //音を鳴らすとか・・
                    }
                    if (!m_OpTextureMap.ContainsKey(skeletonData[i].TrackingId))
                    {
                        //撮影する
                    }
                }
            }

            

            base.Update(gameTime);
        }


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
            foreach (var pass in effectColor.CurrentTechnique.Passes)
            {
                pass.Apply();
                drawPointCloud();
            }
            renderingTexture = 1 - renderingTexture;
            effectNormalTexture.Texture = kinectColorTextures[renderingTexture];
            for (int i = 0; i < skeletonData.Length; ++i)
            {
                if (skeletonData[i].TrackingState == SkeletonTrackingState.Tracked)
                {
                    m_OpList[i].prepareDraw(GraphicsDevice);
                }
            }
            foreach (var pass in effectNormalTexture.CurrentTechnique.Passes)
            {
                pass.Apply();
                for (int i = 0; i < skeletonData.Length; ++i)
                {
                    if (skeletonData[i].TrackingState == SkeletonTrackingState.Tracked)
                    {
                        m_OpList[i].drawPass(GraphicsDevice, effectNormalTexture);
                    }
                }
            }
            base.Draw(gameTime);
        }
    }
}
