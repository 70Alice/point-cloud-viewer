using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;

using laszip.net;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using OpenTK.Platform;

namespace byWednesday
{
    struct Point3DExt
    {
        public Vector3d point;
        public double flag;
    }
    public partial class Form1 : Form
    {
        #region 类成员变量
        bool bOpenGLInitial = false;//确保OpenGL已经初始化
        
        int ptSize = 1;//点的大小
        
        double transX = 0; //平移尺寸
        double transY = 0;
        
        double angleX = 0; //旋转角度
        double angleY = 0;
        
        float scaling = 1.0f;//图形大小
        
        string paint_object = "las";//打开文件
        
        bool show_octree_outline = false;//是否画包围盒
        
        double[,] mFrustum = new double[6, 4];//视景体
        
        bool bLeftButtonPushed = false;//鼠标状态
        bool bRightButtonPushed = false;

        Point leftButtonPosition; //Point是C#的类，管理三维
        Point RightButtonPosition;

        PointCloudOctree pco;//八叉树对象

        string point_cloud_color = "rainbow";//选择渲染颜色

        bool calculate_distance_between_points = false;//是否计算两点距离
        List<Point3DExt> two_points = new List<Point3DExt>(2);//长度为2，存放要求距离的点
        int num_two_points = -1;//判断选中的点是第几个

        //投影相关
        float fov = (float)Math.PI / 3.0f; //视角
        bool perspective_projection = false; //选择投影方式，默认正交

        #endregion

        #region 窗体函数
        public Form1()
        {
            InitializeComponent();
            glControl1.MouseWheel += new MouseEventHandler(glControl1_MouseWheel);//鼠标滚轮事件

            //double[,] mFrustum = new double[6, 4];//视景体
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InitialGL();
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Render(ptSize, show_octree_outline, point_cloud_color);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (bOpenGLInitial)
            {
                SetupViewport();
                Invalidate();//刷新 如果是glcontrolPaint glControl1.Invalidate
            }
        }
        
        #endregion

        #region Draw函数
        private void DrawTriangle()
        {
            /*新旧版OpenTK语法有差别（设置要画的是什么物体，这里是线构成的环状线条，
             * Begin和end一起出现）*/
            GL.Begin(PrimitiveType.Triangles);
            GL.Color4(Color4.Yellow);
            GL.Vertex3(0, 0, 0);
            GL.Color4(Color4.Red);
            GL.Vertex3(0.9, 0, 0);
            GL.Color4(Color4.Green);
            GL.Vertex3(0.9, 0.9, 0);
            GL.End();
        }

        private void DrawSphere()
        {
            const double radius = 0.5;
            const int step = 5;
            int xWidth = 360 / step + 1;
            int zHeight = 180 / step + 1;
            int halfZHeight = (zHeight - 1) / 2;
            int v = 0;
            double xx, yy, zz;

            GL.PointSize(ptSize);
            GL.Begin(PrimitiveType.Points);
            GL.Color4(Color4.Yellow);
            for (int z = -halfZHeight; z <= halfZHeight; z++)
            {
                var d = 0;
                for (int x = 0; x < xWidth; x++)
                {
                    xx = radius * Math.Cos(x * step * Math.PI / 180)
                        * Math.Cos(z * step * Math.PI / 180.0);
                    zz = radius * Math.Sin(x * step * Math.PI / 180)
                        * Math.Cos(z * step * Math.PI / 180.0);
                    yy = radius * Math.Sin(z * step * Math.PI / 180);
                    GL.Vertex3(xx, yy, zz);
                }
            }
            GL.End();
        }

        private void DrawPointClout()
        {
            /*if (points == null)
                return;

            GL.PointSize(ptSize);//控制点的大小

            GL.Begin(PrimitiveType.Points);

            for (int i = 0; i < points.Count; i++)
            {
                Vector3d cr = colors[i];
                Vector3d v = points[i];

                GL.Color3(cr.X, cr.Y, cr.Z);
                GL.Vertex3(v.X, v.Y, v.Z);
            }

            GL.End();*/
        }
        #endregion

        #region 基本功能，绘制渲染
        private void InitialGL()
        {
            GL.ShadeModel(ShadingModel.Smooth); // 启用平滑渲染。默认
            GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f); // 黑色背景。默认
            GL.ClearDepth(1.0f); // 设置深度缓存。默认1
            GL.Enable(EnableCap.DepthTest); // 启用深度测试。默认关闭
            SetupViewport();

            bOpenGLInitial = true;
        }

        private void SetupViewport()
        {
            int w = glControl1.ClientSize.Width;
            int h = glControl1.ClientSize.Height;

            GL.MatrixMode(MatrixMode.Projection); // 后面将对投影做操作
            GL.LoadIdentity();

            double aspect;

            if (perspective_projection) //透视投影
            {
                aspect = w / (double)h;
                like_gluPerspective(fov, aspect, 0.001, 10);
                GL.Viewport(0, 0, w, h);
            }
            else //正交投影
            {
                aspect = (w >= h) ? (1.0 * w / h) : (1.0 * h / w);
                if (w <= h)
                    GL.Ortho(-1, 1, -aspect, aspect, -1, 1); //宽小于高，扩大Y
                else
                    GL.Ortho(-aspect, aspect, -1, 1, -1, 1); //宽大于高，扩大X
                GL.Viewport(0, 0, w, h);
            }
        }

        private void Render(int point_size, bool ShowOctreeOutline, string PointCloudColor)//（绘图）
        {
            glControl1.MakeCurrent(); //后续OpenGL显示操作在当前控件窗口内进行
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //清除当前帧已有内容，清除深度测试缓冲区

            //绘图矩阵
            GL.MatrixMode(MatrixMode.Projection); // 后面将对投影做操作（矩阵操作对投影）
            GL.LoadIdentity();//（调用单位矩阵）

            //图形变换
            if (perspective_projection)
                GL.Translate(transX, transY, -1); //平移
            else
                GL.Translate(transX, transY, 0);
            GL.Rotate(angleY, 1, 0, 0); //旋转
            GL.Rotate(angleX, 0, 1, 0);
            GL.Scale(scaling, scaling, scaling);//缩放

            CalculateFrustum();

            //绘图函数
            if (paint_object == "las")
            {
                if (pco != null)
                {
                    pco.Render(point_size, ShowOctreeOutline, PointCloudColor, mFrustum);//现在还没有，八叉树自己的绘图函数
                }
            }
            else if (paint_object == "sphere")
            {
                DrawSphere();
            }
            else if (paint_object == "triangle")
            {
                DrawTriangle();
            }


            glControl1.SwapBuffers(); /*交换缓冲区。双缓冲绘制时，所有的绘制都是绘制到后台缓冲区里，如果不交换缓冲区，就看不到绘制内容。OpenTK 默认双缓冲（不让屏幕产生晃动，先画到内存缓冲区，再到屏幕）*/
        }

        #endregion

        #region 视景体
        public void CalculateFrustum()
        {
            Matrix4 projectionMatrix = new Matrix4();
            GL.GetFloat(GetPName.ProjectionMatrix, out projectionMatrix);
            Matrix4 modelViewMatrix = new Matrix4();
            GL.GetFloat(GetPName.ModelviewMatrix, out modelViewMatrix);

            float[] _clipMatrix = new float[16];
            const int RIGHT = 0, LEFT = 1, BOTTOM = 2, TOP = 3, BACK = 4, FRONT = 5;

            _clipMatrix[0] = (modelViewMatrix.M11 * projectionMatrix.M11)
                + (modelViewMatrix.M12 * projectionMatrix.M21) + (modelViewMatrix.M13 * projectionMatrix.M31)
                + (modelViewMatrix.M14 * projectionMatrix.M41);
            _clipMatrix[1] = (modelViewMatrix.M11 * projectionMatrix.M12)
                + (modelViewMatrix.M12 * projectionMatrix.M22) + (modelViewMatrix.M13 * projectionMatrix.M32)
                + (modelViewMatrix.M14 * projectionMatrix.M42);
            _clipMatrix[2] = (modelViewMatrix.M11 * projectionMatrix.M13)
                + (modelViewMatrix.M12 * projectionMatrix.M23) + (modelViewMatrix.M13 * projectionMatrix.M33)
                + (modelViewMatrix.M14 * projectionMatrix.M43);
            _clipMatrix[3] = (modelViewMatrix.M11 * projectionMatrix.M14)
                + (modelViewMatrix.M12 * projectionMatrix.M24) + (modelViewMatrix.M13 * projectionMatrix.M34)
                + (modelViewMatrix.M14 * projectionMatrix.M44);

            _clipMatrix[4] = (modelViewMatrix.M21 * projectionMatrix.M11)
                + (modelViewMatrix.M22 * projectionMatrix.M21) + (modelViewMatrix.M23 * projectionMatrix.M31)
                + (modelViewMatrix.M24 * projectionMatrix.M41);
            _clipMatrix[5] = (modelViewMatrix.M21 * projectionMatrix.M12)
                + (modelViewMatrix.M22 * projectionMatrix.M22) + (modelViewMatrix.M23 * projectionMatrix.M32)
                + (modelViewMatrix.M24 * projectionMatrix.M42);
            _clipMatrix[6] = (modelViewMatrix.M21 * projectionMatrix.M13)
                + (modelViewMatrix.M22 * projectionMatrix.M23) + (modelViewMatrix.M23 * projectionMatrix.M33)
                + (modelViewMatrix.M24 * projectionMatrix.M43);
            _clipMatrix[7] = (modelViewMatrix.M21 * projectionMatrix.M14)
                + (modelViewMatrix.M22 * projectionMatrix.M24) + (modelViewMatrix.M23 * projectionMatrix.M34)
                + (modelViewMatrix.M24 * projectionMatrix.M44);

            _clipMatrix[8] = (modelViewMatrix.M31 * projectionMatrix.M11)
                + (modelViewMatrix.M32 * projectionMatrix.M21) + (modelViewMatrix.M33 * projectionMatrix.M31)
                + (modelViewMatrix.M34 * projectionMatrix.M41);
            _clipMatrix[9] = (modelViewMatrix.M31 * projectionMatrix.M12)
                + (modelViewMatrix.M32 * projectionMatrix.M22) + (modelViewMatrix.M33 * projectionMatrix.M32)
                + (modelViewMatrix.M34 * projectionMatrix.M42);
            _clipMatrix[10] = (modelViewMatrix.M31 * projectionMatrix.M13)
                + (modelViewMatrix.M32 * projectionMatrix.M23) + (modelViewMatrix.M33 * projectionMatrix.M33)
                + (modelViewMatrix.M34 * projectionMatrix.M43);
            _clipMatrix[11] = (modelViewMatrix.M31 * projectionMatrix.M14)
                + (modelViewMatrix.M32 * projectionMatrix.M24) + (modelViewMatrix.M33 * projectionMatrix.M34)
                + (modelViewMatrix.M34 * projectionMatrix.M44);

            _clipMatrix[12] = (modelViewMatrix.M41 * projectionMatrix.M11)
                + (modelViewMatrix.M42 * projectionMatrix.M21) + (modelViewMatrix.M43 * projectionMatrix.M31)
                + (modelViewMatrix.M44 * projectionMatrix.M41);
            _clipMatrix[13] = (modelViewMatrix.M41 * projectionMatrix.M12)
                + (modelViewMatrix.M42 * projectionMatrix.M22) + (modelViewMatrix.M43 * projectionMatrix.M32)
                + (modelViewMatrix.M44 * projectionMatrix.M42);
            _clipMatrix[14] = (modelViewMatrix.M41 * projectionMatrix.M13)
                + (modelViewMatrix.M42 * projectionMatrix.M23) + (modelViewMatrix.M43 * projectionMatrix.M33)
                + (modelViewMatrix.M44 * projectionMatrix.M43);
            _clipMatrix[15] = (modelViewMatrix.M41 * projectionMatrix.M14)
                + (modelViewMatrix.M42 * projectionMatrix.M24) + (modelViewMatrix.M43 * projectionMatrix.M34)
                + (modelViewMatrix.M44 * projectionMatrix.M44);

            mFrustum[RIGHT, 0] = _clipMatrix[3] - _clipMatrix[0];
            mFrustum[RIGHT, 1] = _clipMatrix[7] - _clipMatrix[4];
            mFrustum[RIGHT, 2] = _clipMatrix[11] - _clipMatrix[8];
            mFrustum[RIGHT, 3] = _clipMatrix[15] - _clipMatrix[12];
            NormalizePlane(mFrustum, RIGHT);

            mFrustum[LEFT, 0] = _clipMatrix[3] + _clipMatrix[0];
            mFrustum[LEFT, 1] = _clipMatrix[7] + _clipMatrix[4];
            mFrustum[LEFT, 2] = _clipMatrix[11] + _clipMatrix[8];
            mFrustum[LEFT, 3] = _clipMatrix[15] + _clipMatrix[12];
            NormalizePlane(mFrustum, LEFT);

            mFrustum[BOTTOM, 0] = _clipMatrix[3] + _clipMatrix[1];
            mFrustum[BOTTOM, 1] = _clipMatrix[7] + _clipMatrix[5];
            mFrustum[BOTTOM, 2] = _clipMatrix[11] + _clipMatrix[9];
            mFrustum[BOTTOM, 3] = _clipMatrix[15] + _clipMatrix[13];
            NormalizePlane(mFrustum, BOTTOM);

            mFrustum[TOP, 0] = _clipMatrix[3] - _clipMatrix[1];
            mFrustum[TOP, 1] = _clipMatrix[7] - _clipMatrix[5];
            mFrustum[TOP, 2] = _clipMatrix[11] - _clipMatrix[9];
            mFrustum[TOP, 3] = _clipMatrix[15] - _clipMatrix[13];
            NormalizePlane(mFrustum, TOP);

            mFrustum[BACK, 0] = _clipMatrix[3] - _clipMatrix[2];
            mFrustum[BACK, 1] = _clipMatrix[7] - _clipMatrix[6];
            mFrustum[BACK, 2] = _clipMatrix[11] - _clipMatrix[10];
            mFrustum[BACK, 3] = _clipMatrix[15] - _clipMatrix[14];
            NormalizePlane(mFrustum, BACK);

            mFrustum[FRONT, 0] = _clipMatrix[3] + _clipMatrix[2];
            mFrustum[FRONT, 1] = _clipMatrix[7] + _clipMatrix[6];
            mFrustum[FRONT, 2] = _clipMatrix[11] + _clipMatrix[10];
            mFrustum[FRONT, 3] = _clipMatrix[15] + _clipMatrix[14];
            NormalizePlane(mFrustum, FRONT);
        }

        private void NormalizePlane(double[,] frustum, int side)
        {
            double magnitude = Math.Sqrt((frustum[side, 0] * frustum[side, 0]) +
           (frustum[side, 1] * frustum[side, 1]) + (frustum[side, 2] * frustum[side, 2]));
            frustum[side, 0] /= magnitude;
            frustum[side, 1] /= magnitude;
            frustum[side, 2] /= magnitude;
            frustum[side, 3] /= magnitude;
        }

        #endregion

        #region 鼠标操作
        private void glControl1_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                bLeftButtonPushed = true;
                leftButtonPosition = e.Location;
            }
            else if (e.Button == MouseButtons.Right)
            {
                bRightButtonPushed = true;
                RightButtonPosition = e.Location;
            }
        }

        private void glControl1_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            bLeftButtonPushed = false;
            bRightButtonPushed = false;
        }

        private void glControl1_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (bLeftButtonPushed)//左键控制平移物体
            {
                transX += (e.Location.X - leftButtonPosition.X) / 120.0; //比例自己试
                transY += -(e.Location.Y - leftButtonPosition.Y) / 120.0;
                leftButtonPosition = e.Location;
                Invalidate();
            }
            if (bRightButtonPushed)//右键控制旋转物体
            {
                angleX += (e.Location.X - RightButtonPosition.X) / 10.0;
                angleY += -(e.Location.Y - RightButtonPosition.Y) / 10.0;
                RightButtonPosition = e.Location;
                Invalidate();
            }
        }

        //鼠标滚轮
        private void glControl1_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Delta > 0) //e.delta表示鼠标前后滚
            {
                scaling += 0.1f;
            }
            else if (e.Delta < 0)
            {
                scaling -= 0.1f;
            }
            SetupViewport();
            Invalidate(); 
        }

        private void glControl1_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            Point ptClicked = e.Location;
            //射线选择
            Vector3d winxyz;
            winxyz.X = ptClicked.X;
            winxyz.Y = ptClicked.Y;
            winxyz.Z = 0.0f;
            Vector3d nearPoint = new Vector3d(0, 0, 0);
            UnProject(winxyz, ref nearPoint);
            winxyz.Z = 1.0f;
            Vector3d farPoint = new Vector3d(0, 0, 0);
            UnProject(winxyz, ref farPoint);

            Vector3d line;
            line = farPoint - nearPoint;

            Point3DExt close_point = new Point3DExt();
            close_point.flag = 10000;

            if (pco != null)
            {
                pco.FindClosestPoint(mFrustum, nearPoint, farPoint, ref close_point);
                if (!calculate_distance_between_points)
                    MessageBox.Show("选中点坐标为：\nX坐标：" + close_point.point.X
                        + "\nY坐标：" + close_point.point.Y + "\nZ坐标：" + close_point.point.Z);
                else
                {
                    if (num_two_points > 0)
                    {
                        num_two_points = 0;
                        two_points.Clear();
                    }
                    else
                        num_two_points += 1;

                    two_points.Add(close_point);
                    if (num_two_points == 1)
                    {
                        double dis_points = calculateDistance(two_points[0], two_points[1]);
                        MessageBox.Show("选取的两点坐标为：\n"
                            + coordinate2string(two_points[0].point) + "\n"
                            + coordinate2string(two_points[1].point) + "\n"
                            + "这两点之间的距离为：\n"
                            + Convert.ToString(dis_points));//distance
                    }
                }
                Render(ptSize, show_octree_outline, point_cloud_color);
                Invalidate();
                glControl1.Invalidate();
            }
        }
        #endregion

        #region 打开点云文件
        private void 打开OToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //新建一个文件对话框
            OpenFileDialog pOpenFileDialog = new OpenFileDialog();

            //设置对话框标题
            pOpenFileDialog.Title = "打开las点云文件";

            //设置打开文件类型
            pOpenFileDialog.Filter = "las文件（*.las）|*.las";

            //监测文件是否存在
            pOpenFileDialog.CheckFileExists = true;

            //文件打开后执行以下程序
            if (pOpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                //MessageBox.Show(pOpenFileDialog.FileName);
                pco = ReadLas(pOpenFileDialog.FileName);//传入文件路径，读出点云文件
                Invalidate();
            }
        }

        private PointCloudOctree ReadLas(string fileName)
        {
            var lazReader = new laszip_dll();
            var compressed = true;
            lazReader.laszip_open_reader(fileName, ref compressed); //FileName要给定
            var numberOfPoints = lazReader.header.number_of_point_records;

            // las文件中三维点的范围
            double minx = lazReader.header.min_x;
            double miny = lazReader.header.min_y;
            double minz = lazReader.header.min_z;
            double maxx = lazReader.header.max_x;
            double maxy = lazReader.header.max_y;
            double maxz = lazReader.header.max_z;

            double centx = (minx + maxx) / 2;
            double centy = (miny + maxy) / 2;
            double centz = (minz + maxz) / 2;

            double scale = Math.Max(Math.Max(maxx - minx, maxy - miny), (maxz - minz));//0715上课说要再除以2

            int classification = 0;
            var coordArray = new double[3];//自己考虑是否需要double float
            ColorPoint color_point = new ColorPoint();//vector3d是double vector is float
            List<ColorPoint> points = new List<ColorPoint>((int)numberOfPoints);

            //循环读取每个点
            for (int pointIndex = 0; pointIndex < numberOfPoints; pointIndex++)
            {
                // 读点
                lazReader.laszip_read_point();
                // 得到坐标值
                lazReader.laszip_get_coordinates(coordArray);

                //彩虹色
                Vector3d cr_red = new Vector3d(1, 0, 0);
                Vector3d cr_orange = new Vector3d(1, 0.647, 0);
                Vector3d cr_yellow = new Vector3d(1, 1, 0);
                Vector3d cr_green = new Vector3d(0, 1, 0);
                Vector3d cr_cyan = new Vector3d(0, 0.498, 1);
                Vector3d cr_bule = new Vector3d(0, 0, 1);
                Vector3d cr_purple = new Vector3d(0.545, 0, 1);

                //每个点根据坐标着色
                if (point_cloud_color == "rainbow")
                {
                    //6等分z轴,用于插值颜色
                    double z_1_6 = 1 * (maxz - minz) / 6 + minz;
                    double z_2_6 = 2 * (maxz - minz) / 6 + minz;
                    double z_3_6 = 3 * (maxz - minz) / 6 + minz;
                    double z_4_6 = 4 * (maxz - minz) / 6 + minz;
                    double z_5_6 = 5 * (maxz - minz) / 6 + minz;

                    if (coordArray[2] <= z_1_6)
                    {
                        color_point.color.X = (coordArray[2] - minz) / (z_1_6 - minz) * (cr_orange.X - cr_red.X) + cr_red.X;
                        color_point.color.Y = (coordArray[2] - minz) / (z_1_6 - minz) * (cr_orange.Y - cr_red.Y) + cr_red.Y;
                        color_point.color.Z = (coordArray[2] - minz) / (z_1_6 - minz) * (cr_orange.Z - cr_red.Z) + cr_red.Z;
                    }
                    else if (coordArray[2] <= z_2_6)
                    {
                        color_point.color.X = (coordArray[2] - z_1_6) / (z_2_6 - z_1_6) * (cr_yellow.X - cr_orange.X) + cr_orange.X;
                        color_point.color.Y = (coordArray[2] - z_1_6) / (z_2_6 - z_1_6) * (cr_yellow.Y - cr_orange.Y) + cr_orange.Y;
                        color_point.color.Z = (coordArray[2] - z_1_6) / (z_2_6 - z_1_6) * (cr_yellow.Z - cr_orange.Z) + cr_orange.Z;
                    }
                    else if (coordArray[2] <= z_3_6)
                    {
                        color_point.color.X = (coordArray[2] - z_2_6) / (z_3_6 - z_2_6) * (cr_green.X - cr_yellow.X) + cr_yellow.X;
                        color_point.color.Y = (coordArray[2] - z_2_6) / (z_3_6 - z_2_6) * (cr_green.Y - cr_yellow.Y) + cr_yellow.Y;
                        color_point.color.Z = (coordArray[2] - z_2_6) / (z_3_6 - z_2_6) * (cr_green.Z - cr_yellow.Z) + cr_yellow.Z;
                    }
                    else if (coordArray[2] <= z_4_6)
                    {
                        color_point.color.X = (coordArray[2] - z_3_6) / (z_4_6 - z_3_6) * (cr_cyan.X - cr_green.X) + cr_green.X;
                        color_point.color.Y = (coordArray[2] - z_3_6) / (z_4_6 - z_3_6) * (cr_cyan.Y - cr_green.Y) + cr_green.Y;
                        color_point.color.Z = (coordArray[2] - z_3_6) / (z_4_6 - z_3_6) * (cr_cyan.Z - cr_green.Z) + cr_green.Z;
                    }
                    else if (coordArray[2] <= z_5_6)
                    {
                        color_point.color.X = (coordArray[2] - z_4_6) / (z_5_6 - z_4_6) * (cr_bule.X - cr_cyan.X) + cr_cyan.X;
                        color_point.color.Y = (coordArray[2] - z_4_6) / (z_5_6 - z_4_6) * (cr_bule.Y - cr_cyan.Y) + cr_cyan.Y;
                        color_point.color.Z = (coordArray[2] - z_4_6) / (z_5_6 - z_4_6) * (cr_bule.Z - cr_cyan.Z) + cr_cyan.Z;
                    }
                    else
                    {
                        color_point.color.X = (coordArray[2] - z_5_6) / (maxz - z_5_6) * (cr_purple.X - cr_bule.X) + cr_bule.X;
                        color_point.color.Y = (coordArray[2] - z_5_6) / (maxz - z_5_6) * (cr_purple.Y - cr_bule.Y) + cr_bule.Y;
                        color_point.color.Z = (coordArray[2] - z_5_6) / (maxz - z_5_6) * (cr_purple.Z - cr_bule.Z) + cr_bule.Z;
                    }
                }

                else if (point_cloud_color == "warm")
                {
                    //2等分z轴,用于插值颜色
                    double z_1_2 = 1 * (maxz - minz) / 2 + minz;

                    if (coordArray[2] <= z_1_2)
                    {
                        color_point.color.X = (coordArray[2] - minz) / (z_1_2 - minz) * (cr_orange.X - cr_red.X) + cr_red.X;
                        color_point.color.Y = (coordArray[2] - minz) / (z_1_2 - minz) * (cr_orange.Y - cr_red.Y) + cr_red.Y;
                        color_point.color.Z = (coordArray[2] - minz) / (z_1_2 - minz) * (cr_orange.Z - cr_red.Z) + cr_red.Z;
                    }
                    else
                    {
                        color_point.color.X = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (cr_yellow.X - cr_orange.X) + cr_orange.X;
                        color_point.color.Y = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (cr_yellow.Y - cr_orange.Y) + cr_orange.Y;
                        color_point.color.Z = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (cr_yellow.Z - cr_orange.Z) + cr_orange.Z;
                    }
                }
                else if (point_cloud_color == "cold")
                {
                    //2等分z轴,用于插值颜色
                    double z_1_2 = 1 * (maxz - minz) / 2 + minz;

                    if (coordArray[2] <= z_1_2)
                    {
                        color_point.color.X = (coordArray[2] - minz) / (z_1_2 - minz) * (cr_cyan.X - cr_green.X) + cr_green.X;
                        color_point.color.Y = (coordArray[2] - minz) / (z_1_2 - minz) * (cr_cyan.Y - cr_green.Y) + cr_green.Y;
                        color_point.color.Z = (coordArray[2] - minz) / (z_1_2 - minz) * (cr_cyan.Z - cr_green.Z) + cr_green.Z;
                    }
                    else
                    {
                        color_point.color.X = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (cr_bule.X - cr_cyan.X) + cr_cyan.X;
                        color_point.color.Y = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (cr_bule.Y - cr_cyan.Y) + cr_cyan.Y;
                        color_point.color.Z = (coordArray[2] - z_1_2) / (maxz - z_1_2) * (cr_bule.Z - cr_cyan.Z) + cr_cyan.Z;
                    }
                }

                color_point.point.X = (coordArray[0] - centx) / scale; //归一化
                color_point.point.Y = (coordArray[1] - centy) / scale;
                color_point.point.Z = (coordArray[2] - centz) / scale;

                //points.Add(point);
                points.Add(color_point);
                classification = lazReader.point.classification;
            }
            // 关闭
            lazReader.laszip_close_reader();

            Vector3d minv;
            minv.X = (minx - centx) / scale;
            minv.Y = (miny - centy) / scale;
            minv.Z = (minz - centz) / scale;

            Vector3d maxv;
            maxv.X = (maxx - centx) / scale;
            maxv.Y = (maxy - centy) / scale;
            maxv.Z = (maxz - centz) / scale;

            PointCloudOctree p = new PointCloudOctree(points, minv, maxv);
            return p;
        }
        #endregion

        #region 扩展功能

        #region 鼠标选点
        int UnProject(Vector3d win, ref Vector3d obj)
        {
            Matrix4d modelMatrix;
            GL.GetDouble(GetPName.ModelviewMatrix, out modelMatrix);
            Matrix4d projMatrix;
            GL.GetDouble(GetPName.ProjectionMatrix, out projMatrix);
            int[] viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);
            return UnProject(win, modelMatrix, projMatrix, viewport, ref obj);
        }

        int UnProject(Vector3d win, Matrix4d modelMatrix, Matrix4d projMatrix, int[] viewport, ref Vector3d obj)
        {
            return like_gluUnProject(win.X, win.Y, win.Z, modelMatrix, projMatrix,
            viewport, ref obj.X, ref obj.Y, ref obj.Z);
        }

        int like_gluUnProject(double winx, double winy, double winz,
            Matrix4d modelMatrix, Matrix4d projMatrix, int[] viewport,
            ref double objx, ref double objy, ref double objz)
        {
            Matrix4d finalMatrix;
            Vector4d _in;
            Vector4d _out;
            finalMatrix = Matrix4d.Mult(modelMatrix, projMatrix);
            finalMatrix.Invert();
            _in.X = winx;
            _in.Y = viewport[3] - winy;
            _in.Z = winz;
            _in.W = 1.0f;
            // Map x and y from window coordinates
            _in.X = (_in.X - viewport[0]) / viewport[2];
            _in.Y = (_in.Y - viewport[1]) / viewport[3];
            // Map to range -1 to 1
            _in.X = _in.X * 2 - 1;
            _in.Y = _in.Y * 2 - 1;
            _in.Z = _in.Z * 2 - 1;
            //__gluMultMatrixVecd(finalMatrix, _in, _out);
            // check if this works:
            _out = Vector4d.Transform(_in, finalMatrix);
            if (_out.W == 0.0)
                return (0);
            _out.X /= _out.W;
            _out.Y /= _out.W;
            _out.Z /= _out.W;
            objx = _out.X;
            objy = _out.Y;
            objz = _out.Z;
            return (1);
        }
        #endregion

        #region 截图
        private void 截图ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int[] vdata = new int[4];
            GL.GetInteger(GetPName.Viewport, vdata);
            int w = vdata[2];
            int h = vdata[3];
            if ((w % 4) != 0)
                w = (w / 4 + 1) * 4;
            byte[] imgBuffer = new byte[w * h * 3];
            GL.ReadPixels(0, 0, w, h, OpenTK.Graphics.OpenGL.PixelFormat.Bgr,
           PixelType.UnsignedByte, imgBuffer);
            FlipHeight(imgBuffer, w, h);
            Bitmap bmp = BytesToImg(imgBuffer, w, h);
            bmp.Save("D:\\opentk.bmp");
            MessageBox.Show("截图成功！");
        }
        private void FlipHeight(byte[] data, int w, int h)
        {
            int wstep = w * 3;
            byte[] temp = new byte[wstep];
            for (int i = 0; i < h / 2; i++)
            {
                Array.Copy(data, wstep * i, temp, 0, wstep);
                Array.Copy(data, wstep * (h - i - 1), data, wstep * i, wstep);
                Array.Copy(temp, 0, data, wstep * (h - i - 1), wstep);
            }
        }
        private Bitmap BytesToImg(byte[] bytes, int w, int h)
        {
            Bitmap bmp = new Bitmap(w, h);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h),
            ImageLockMode.ReadWrite,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            IntPtr ptr = bd.Scan0;
            int bmpLen = bd.Stride * bd.Height;
            Marshal.Copy(bytes, 0, ptr, bmpLen); //using System.Runtime.InteropServices;
            bmp.UnlockBits(bd);
            return bmp;
        }
        #endregion

        #region 选择色系
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            point_cloud_color = "rainbow";
            Invalidate();

        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            point_cloud_color = "warm";
            Invalidate();
            //Render(ptSize, show_octree_outline, point_cloud_color);
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            point_cloud_color = "cold";
            Invalidate();
            //Render(ptSize, show_octree_outline, point_cloud_color);
        }
        #endregion

        #region 选择绘制类型
        private void 绘制三角形ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            paint_object = "triangle";
        }

        private void 绘制球体ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            paint_object = "sphere";
        }
        #endregion

        #region 显示包围核
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (show_octree_outline == false)
                show_octree_outline = true;
            else
                show_octree_outline = false;

            Invalidate();
        }
        #endregion

        #region 选择点大小
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (textBox1.Text == "")
            {
                return;
            }

            int a = Int32.Parse(textBox1.Text);

            if (a <= 0 || a >= 10)
            {
                a = 1;
            }

            ptSize = a;

            Invalidate();
        }
        #endregion

        #region 求两点间距离
        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (calculate_distance_between_points)
                calculate_distance_between_points = false;
            else
                calculate_distance_between_points = true;
        }

        private string coordinate2string(Vector3d v)//将点的坐标转换为字符串以输出
        {
            string string_of_coordinate = "(" + Convert.ToString(v.X) + "," + Convert.ToString(v.Y) + ","
                + Convert.ToString(v.Z) + ")";
            return string_of_coordinate;
        }

        private double calculateDistance(Point3DExt point1, Point3DExt point2)
        {
            double x = point1.point.X - point2.point.X;
            double y = point1.point.Y - point2.point.Y;
            double z = point1.point.Z - point2.point.Z;

            double dd = x * x + y * y + z * z;
            double d = Math.Sqrt(dd);

            return d;
        }

        #endregion

        #region 使用说明
        private void 使用说明ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("登陆后，从打开菜单中选择绘制的图像\n" +
                "鼠标左键可以控制物体平移\n" +
                "鼠标右键可以控制物体旋转\n" +
                "鼠标滚轮可以控制物体缩放\n" +
                "双击鼠标可以选点" +
                "复选框可以选择绘制包围核（默认不绘制），也可以选择计算两点间距离（默认不计算）");
        }
        #endregion

        #region 打开新文件与退出
        private void button4_Click(object sender, EventArgs e)
        {
            this.Hide();
            Form1 form1 = new Form1();
            form1.Show();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        #endregion

        #region 投影方式
        public void like_gluPerspective(double fovy, double aspect, double near, double far)
        {
            const double DEG2RAD = 3.14159265 / 180.0;
            double tangent = Math.Tan(fovy / 2 * DEG2RAD);
            double height = near * tangent;
            double width = height * aspect;
            GL.Frustum(-width, width, -height, height, near, far);
        }
        private void radioButton4_CheckedChanged_1(object sender, EventArgs e)
        {
            perspective_projection = false;
            Invalidate();
        }

        private void radioButton5_CheckedChanged_1(object sender, EventArgs e)
        {
            perspective_projection = true;
            Invalidate();
        }
        #endregion


        #endregion

        #region useless
        private void radioButton5_CheckedChanged(object sender, EventArgs e)
        {
            
        }

        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            
        }
        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            //richTextBox1.Text = "ok";
        }

        private void label1_Click(object sender, EventArgs e)
        {
            //label1.Text = "a";
        }

        private void splitContainer2_Panel2_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {

        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            ptSize += 1;
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            
        }

        private void checkBox3_CheckedChanged_1(object sender, EventArgs e)
        {

        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = false;
        }
        #endregion


    }
}
