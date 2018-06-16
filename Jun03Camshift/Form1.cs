using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
//*************************EMGU部分
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Emgu.Util;

namespace Jun03Camshift
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private Capture capture;
        private bool isTrack = false;
        private Image<Gray, byte> hue = null;//色度
        private Image<Gray, byte> mask = null;
        private Image<Gray, byte> backproject = null;//反向投影
        private Image<Hsv, byte> hsv = null;//颜色空间
        private Image<Gray, Byte> gray = null;//灰度
        private Rectangle trackwin;
        private IntPtr[] img = null;
        private Point startPos;
        private Point endPos;
        private Point LastendPos;
        private bool _captureInProgress;
        private MCvBox2D trackbox = new MCvBox2D();
        private MCvConnectedComp trackcomp = new MCvConnectedComp();
        private DenseHistogram hist = new DenseHistogram(16, new RangeF(0, 180));
        static Image<Bgr, Byte> frame;
        private void ProcessFrame(object sender, EventArgs arg)
        {
            //获取图像
            frame = capture.QueryFrame();
            //如果获取到的为空
            if (frame != null)
            {
                #region//图像处理代码
                //思路
                /*第一步：选中物体，记录你输入的方框和物体。

                第二步：求出视频中有关物体的反向投影图。

                第三步：根据反向投影图和输入的方框进行meanshift迭代，由于它是向重心移动，即向反向投影图中概率大的地方移动，所以始终会移动到目标上。

                第四步：然后下一帧图像时用上一帧输出的方框来迭代即可。*/
                //************************************************这里开始用到camshift，算是核心吧*********************************************************
                //2.9版本和3.0版本不一样 以前是image 现在是mat
                //嗯 是的 屈服于新版本资料太少之下，改为2.1.0
                gray = frame.Convert<Gray, Byte>();
                gray._EqualizeHist();
                hue = new Image<Gray, byte>(frame.Width, frame.Height);//目标色度
                mask = new Image<Gray, byte>(frame.Width, frame.Height);
                backproject = new Image<Gray, byte>(frame.Width, frame.Height);
                hsv = frame.Convert<Hsv, byte>();  //彩色空间转换从BGR到HSV
                hsv._EqualizeHist();
                //比较是否在两数组中间的值，是否在范围内
                Emgu.CV.CvInvoke.cvInRangeS(hsv, new MCvScalar(0, 30, Math.Min(10, 255), 0), new MCvScalar(180, 256, Math.Max(10, 255), 0), mask);
                //分离通道
                Emgu.CV.CvInvoke.cvSplit(hsv, hue, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                if (isTrack == false || endPos != LastendPos)
                {
                    //这里选框可能有点问题，还没修复 所以try掉了
                    try
                    {
                        //比较繁琐的方式，但是简单易懂
                        //获取imagebox上框选的区域起始坐标坐标和大小
                        int width = endPos.X - startPos.X;
                        int height = endPos.Y - startPos.Y;
                        Size picsize = new Size(width, height);
                        Rectangle rc = new Rectangle(startPos, picsize);

                        CvInvoke.cvSetImageROI(hue, rc);  // 设置为ROI
                        CvInvoke.cvSetImageROI(mask, rc);// 设置为ROI

                        img = new IntPtr[1]
                       {
        hue
                       };

                        Emgu.CV.CvInvoke.cvCalcHist(img, hist, false, mask); //計算直方圖

                        Emgu.CV.CvInvoke.cvResetImageROI(hue); //釋放直方圖
                        Emgu.CV.CvInvoke.cvResetImageROI(mask); //釋放直方圖
                        LastendPos = endPos;
                        trackwin = rc;
                        isTrack = true;
                    }
                    catch (Exception a)
                    { }
                }

                img = new IntPtr[1] { hue };
                if (trackwin.Width > 0 && trackwin.Height > 0)
                {
                    Emgu.CV.CvInvoke.cvCalcBackProject(img, backproject, hist); //计算直方图反向投影
                    Emgu.CV.CvInvoke.cvAnd(backproject, mask, backproject, IntPtr.Zero);//融合
                    Emgu.CV.CvInvoke.cvCamShift(backproject, trackwin, new MCvTermCriteria(10, 0.8), out trackcomp, out trackbox); //使用camshift
                    trackwin = trackcomp.rect;
                    //画出框选范围
                    frame.Draw(trackwin, new Bgr(Color.Red), 2);
                }
                #endregion
                imageBox2.Image = hue;
                imageBox3.Image = mask;
                //输出到界面
                imageBox1.Image = frame;
                //如果是选择本地视频流
                if (radioButton2.Checked)
                {
                    //延迟一下每帧间隔速度，因为这里如果不延迟，本人调试的时候会发现视频明显加速。虽然不是最好的办法，但是先以实现功能为主。
                    Thread.Sleep(30);//每秒30帧
                }
            }
            else
            {
                //释放掉
                capture.Dispose();
                //清空
                imageBox1.Image = null;
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (radioButton1.Checked)
                {
                    capture = new Capture(0);
                }
                else
                {
                    capture = new Capture(Application.StartupPath + @"\demo.mp4");
                    //capture = new Capture("d:\\demo.flv");
                }
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }

            if (capture != null)
            {
                if (_captureInProgress)
                {  //stop the capture
                    label1.Text = "关闭视频";
                    button1.Text = "Open";
                    Application.Idle -= new EventHandler(ProcessFrame);
                    imageBox1.Image = null;
                }
                else
                {
                    //start the capture
                    label1.Text = "打开视频";
                    button1.Text = "Close";
                    Application.Idle += new EventHandler(ProcessFrame);
                    //capture.Dispose();
                }
                _captureInProgress = !_captureInProgress;
            }
        }
        private void imageBox1_MouseDown(object sender, MouseEventArgs e)
        {
            startPos = new Point(e.X, e.Y);
            Console.WriteLine("DOWN!" + startPos);//调试用
        }
        private void imageBox1_MouseUp(object sender, MouseEventArgs e)
        {
            endPos = new Point(e.X, e.Y);
            Console.WriteLine("UP!" + endPos);//调试用
        }
    }
}