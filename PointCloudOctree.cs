using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using OpenTK.Platform;

namespace byWednesday
{
    struct ColorPoint
    {
        public Vector3d point;
        public Vector3d color;
    }

    class PointCloudNode //结点
    {
        #region 成员变量
        public const int max_point_num = 10000;//每个节点中点的上限

        
        List<ColorPoint> data;//点坐标 颜色
        PointCloudNode[] child;//孩子节点，不需要8个名字

        int iShowListNum;//显示列表

        Vector3d min_coordinate, max_coordinate;//坐标最值

        bool OutlineInFrustum;//包围核是否在视景体内

        string point_cloud_color;//渲染色系
        #endregion
        
        private void DrawNodeOutline(Vector3d min_node_coordinate, Vector3d max_node_coordinate)
        {
            //下面
            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, min_node_coordinate.Y, min_node_coordinate.Z);//(0,0,0)
            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, min_node_coordinate.Y, min_node_coordinate.Z);//(1,0,0)

            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, max_node_coordinate.Y, min_node_coordinate.Z);//(0,1,0)
            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, max_node_coordinate.Y, min_node_coordinate.Z);//(1,1,0)

            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, min_node_coordinate.Y, min_node_coordinate.Z);//(0,0,0)
            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, max_node_coordinate.Y, min_node_coordinate.Z);//(0,1,0)

            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, min_node_coordinate.Y, min_node_coordinate.Z);//(1,0,0)
            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, max_node_coordinate.Y, min_node_coordinate.Z);//(1,1,0)

            GL.End();

            //上面
            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, min_node_coordinate.Y, max_node_coordinate.Z);//(0,0,1)
            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, min_node_coordinate.Y, max_node_coordinate.Z);//(1,0,1)

            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, max_node_coordinate.Y, max_node_coordinate.Z);//(0,1,1)
            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, max_node_coordinate.Y, max_node_coordinate.Z);//(1,1,1)

            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, min_node_coordinate.Y, max_node_coordinate.Z);//(0,0,1)
            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, max_node_coordinate.Y, max_node_coordinate.Z);//(0,1,1)

            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, min_node_coordinate.Y, max_node_coordinate.Z);//(1,0,1)
            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, max_node_coordinate.Y, max_node_coordinate.Z);//(1,1,1)

            GL.End();

            //(0,0,0)-(0,0,1)
            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, min_node_coordinate.Y, min_node_coordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, min_node_coordinate.Y, max_node_coordinate.Z);
            GL.End();

            //(1,1,1)-(1,1,0)
            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, max_node_coordinate.Y, max_node_coordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, max_node_coordinate.Y, min_node_coordinate.Z);
            GL.End();

            //(1,0,0)-(1,0,1)
            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, min_node_coordinate.Y, min_node_coordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(max_node_coordinate.X, min_node_coordinate.Y, max_node_coordinate.Z);
            GL.End();

            //(0,1,0)-(0,1,1)
            GL.Begin(PrimitiveType.Lines);
            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, max_node_coordinate.Y, min_node_coordinate.Z);
            GL.Color4(Color4.White);
            GL.Vertex3(min_node_coordinate.X, max_node_coordinate.Y, max_node_coordinate.Z);
            GL.End();
        }

        public PointCloudNode(List<ColorPoint> data,
            Vector3d minv, Vector3d maxv)
        {
            if (data == null)
                return;
            if (data.Count == 0)
                return;

            if (data.Count < max_point_num)//10000是自己设置的阈值
            {
                this.data = data;//后一个是参数，前一个是成员变量
                min_coordinate = minv;
                max_coordinate = maxv;

                //创建显示列表
                iShowListNum = GL.GenLists(1);
                GL.NewList(iShowListNum, ListMode.Compile);
                GL.Begin(PrimitiveType.Points);
                for(int i=0;i<data.Count;i++)
                {
                    ColorPoint v = data[i];

                    GL.Color3(v.color.X, v.color.Y, v.color.Z);
                    GL.Vertex3(v.point.X, v.point.Y, v.point.Z);
                }
                GL.End();
                GL.EndList();
            }
            else
            {
                this.data = null;
                child = new PointCloudNode[8];

                List<ColorPoint>[] childData = new List<ColorPoint>[8];
                for (int i = 0; i < 8; i++)
                    childData[i] = new List<ColorPoint>();

                Vector3d[] minva = new Vector3d[8];
                Vector3d[] maxva = new Vector3d[8];

                Vector3d split = (minv + maxv) / 2;
                foreach (var v in data)
                {
                    if (v.point.Z > split.Z)//1234节点
                    {
                        if (v.point.Y > split.Y)//12
                        {
                            if (v.point.X > split.X)//1
                            {
                                childData[0].Add(v);
                            }
                            else//2
                            {
                                childData[1].Add(v);
                            }
                        }
                        else//34
                        {
                            if (v.point.X > split.X)//3
                            {
                                childData[2].Add(v);
                            }
                            else//4
                            {
                                childData[3].Add(v);
                            }
                        }
                    }
                    else//5678节点
                    {
                        if (v.point.Y > split.Y)//56
                        {
                            if (v.point.X > split.X)//5
                            {
                                childData[4].Add(v);
                            }
                            else//6
                            {
                                childData[5].Add(v);
                            }
                        }
                        else//78
                        {
                            if (v.point.X > split.X)//7
                            {
                                childData[6].Add(v);
                            }
                            else//8
                            {
                                childData[7].Add(v);
                            }
                        }
                    }
                }
                //重复八次，自己确定最值
                minva[0].X = split.X; minva[0].Y = split.Y; minva[0].Z = split.Z;
                maxva[0].X = maxv.X; maxva[0].Y = maxv.Y; maxva[0].Z = maxv.Z;

                minva[1].X = split.X; minva[1].Y = split.Y; minva[1].Z = split.Z;
                maxva[1].X = split.X; maxva[1].Y = maxv.Y; maxva[1].Z = maxv.Z;

                minva[2].X = split.X; minva[2].Y = minv.Y; minva[2].Z = split.Z;
                maxva[2].X = maxv.X; maxva[2].Y = split.Y; maxva[2].Z = maxv.Z;

                minva[3].X = minv.X; minva[3].Y = minv.Y; minva[3].Z = split.Z;
                maxva[3].X = split.X; maxva[3].Y = split.Y; maxva[3].Z = maxv.Z;

                minva[4].X = split.X; minva[4].Y = split.Y; minva[4].Z = minv.Z;
                maxva[4].X = maxv.X; maxva[4].Y = maxv.Y; maxva[4].Z = split.Z;

                minva[5].X = minv.X; minva[5].Y = split.Y; minva[5].Z = minv.Z;
                maxva[5].X = split.X; maxva[5].Y = maxv.Y; maxva[5].Z = split.Z;

                minva[6].X = split.X; minva[6].Y = minv.Y; minva[6].Z = minv.Z;
                maxva[6].X = maxv.X; maxva[6].Y = split.Y; maxva[6].Z = split.Z;

                minva[7].X = minv.X; minva[7].Y = minv.Y; minva[7].Z = minv.Z;
                maxva[7].X = split.X; maxva[7].Y = split.Y; maxva[7].Z = split.Z;

                for (int i = 0; i < 8; i++)
                {
                    if (childData[i].Count <= 0)
                        continue;
                    child[i] = new PointCloudNode(childData[i], minva[i], maxva[i]);
                }
            }
        }

        public void Render(int point_size, bool ShowOctreeOutline, string PointCloudColor, double[,] frustum)
        {
            OutlineInFrustum = VoxelWithinFrustum(frustum, min_coordinate.X, min_coordinate.Y, min_coordinate.Z,
                max_coordinate.X, max_coordinate.Y, max_coordinate.Z);
            point_cloud_color = PointCloudColor;

            if (!OutlineInFrustum)
            {
                return;
            }

            if (data != null)
            {
                //画点
                GL.PointSize(point_size);
                //调用显示列表
                GL.CallList(iShowListNum);

                if (ShowOctreeOutline == true)
                {
                    DrawNodeOutline(min_coordinate, max_coordinate);
                }
            }

            if (child != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (child[i] != null)
                    {
                        child[i].Render(point_size, ShowOctreeOutline, PointCloudColor, frustum);
                    }
                    //if (ShowOctreeOutline == true)
                    //{
                    //    DrawNodeOutline(min_coordinate, max_coordinate);
                    //}
                }
            }
        }

        //判断视景体
        bool VoxelWithinFrustum(double[,] ftum, double minx, double miny, double minz,
            double maxx, double maxy, double maxz)
        {
            double x1 = minx, y1 = miny, z1 = minz;
            double x2 = maxx, y2 = maxy, z2 = maxz;
            for (int i = 0; i < 6; i++)
            {
                if ((ftum[i, 0] * x1 + ftum[i, 1] * y1 + ftum[i, 2] * z1 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x2 + ftum[i, 1] * y1 + ftum[i, 2] * z1 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x1 + ftum[i, 1] * y2 + ftum[i, 2] * z1 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x2 + ftum[i, 1] * y2 + ftum[i, 2] * z1 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x1 + ftum[i, 1] * y1 + ftum[i, 2] * z2 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x2 + ftum[i, 1] * y1 + ftum[i, 2] * z2 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x1 + ftum[i, 1] * y2 + ftum[i, 2] * z2 + ftum[i, 3] <= 0.0F) &&
                (ftum[i, 0] * x2 + ftum[i, 1] * y2 + ftum[i, 2] * z2 + ftum[i, 3] <= 0.0F))
                {
                    return false;
                }
            }
            return true;
        }

        //找最近点
        public void FindClosestPoint(double[,] frustum, Vector3d near_point, Vector3d far_point,
            ref Point3DExt closest_point)
        {
            if (!OutlineInFrustum)
                return;

            Vector3d line;
            line = far_point - near_point;

            //本节点里查找
            if (data != null)
            {
                //Vector3d v, vcross;
                double distance;

                for(int i = 0; i < data.Count; i++)
                {
                    distance = CalculateDistance(data[i].point, far_point, near_point);

                    if (closest_point.flag > distance)
                    {
                        closest_point.point = data[i].point;
                        closest_point.flag = distance;
                    }
                }
            }
            if (child != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (child[i] != null)
                    {
                        child[i].FindClosestPoint(frustum, near_point, far_point,ref closest_point);
                    }
                }
            }
        }

        //求三角形面积
        private double CalculateSquare(double lenght1, double lenght2, double lenght3)
        {
            double s;

            double half_circumference = (lenght1 + lenght2 + lenght3) / 2;
            double ss = half_circumference * (half_circumference - lenght1)
                * (half_circumference - lenght2) * (half_circumference - lenght3);
            s = Math.Sqrt(ss);

            return s;
        }
        //求两点间距离
        private double CalculateLength(Vector3d point1, Vector3d point2)
        {
            double x = 100 * (point1.X - point2.X);
            double y = 100 * (point1.Y - point2.Y);
            double z = 100 * (point1.Z - point2.Z);

            double ll = x * x + y * y + z * z;
            double l = Math.Sqrt(ll);

            return l;
        }
        //求点到线段距离
        private double CalculateDistance(Vector3d now_point, Vector3d far_point, Vector3d near_point)
        {
            double now_far_distance = CalculateLength(now_point, far_point);
            double now_near_distance = CalculateLength(now_point, near_point);
            double near_far_distance = CalculateLength(near_point, far_point);

            double triangle_square = CalculateSquare(now_far_distance, now_near_distance, near_far_distance);

            double distance = (2 * triangle_square / 10000) / (near_far_distance / 100);

            return distance;
        }
    }

    class PointCloudOctree // 八叉树
    {
        PointCloudNode root;//根节点
        
        //构造函数创建类 参数1：Las文件读出的点 参数2：最小的坐标值 参数3：最大坐标值
        public PointCloudOctree(List<ColorPoint> data,
            Vector3d minv, Vector3d maxv)
        {
            if (data == null)
                return;
            if (data.Count == 0)
                return;

            root = new PointCloudNode(data, minv, maxv);
        }

        //绘制函数
        public void Render(int point_size, bool ShowOctreeOutline, string PointCloudColor, double[,] frustum)
        {
            if (root != null)
                root.Render(point_size, ShowOctreeOutline, PointCloudColor, frustum);
        }
        //找最近点
        public void FindClosestPoint(double[,] frustum, Vector3d near_point, Vector3d far_point,
            ref Point3DExt closest_point)
        {
            if (root != null)
                root.FindClosestPoint(frustum, near_point, far_point, ref closest_point);
        }
    }
}
