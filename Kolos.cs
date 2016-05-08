using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Kolos
{
    public struct pont3d
    {
        public pont3d(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        public double x;
        public double y;
        public double z;
    }
    public static class normalvektor
    {
        private static double[] normalas(double x, double y, double z)
        {
            double hosssz = Math.Sqrt(x * x + y * y + z * z);
            var tomb = new double[3];
            tomb[0] = x / hosssz;
            tomb[1] = y / hosssz;
            tomb[2] = z / hosssz;
            return tomb;
        }
        public static double[] kiszamitas(List<pont3d> lista)
        {
            double sumxx = lista.Sum(p3d => p3d.x * p3d.x);
            double sumyy = lista.Sum(p3d => p3d.y * p3d.y);
            double sumxy = lista.Sum(p3d => p3d.x * p3d.y);
            double sumxz = lista.Sum(p3d => p3d.x * p3d.z);
            double sumyz = lista.Sum(p3d => p3d.y * p3d.z);

            double D = sumxx * sumyy - sumxy * sumxy;
            var tomb = normalas((sumyz * sumxy - sumxz * sumyy) / D, (sumxy * sumxz - sumxx * sumyz) / D, 1);
            
            return tomb;

        }
    }
    public static class haromszog
    {
        /// <summary>
        /// Megadja a háromszög belsõ, egész szám koordinátájú pontjait
        /// </summary>
        /// <param name="csucsok"></param> A háromszög csúcsat tartalmazó lista
        /// <param name="offset"></param> A háromszögön belülü eltolás mértéke, hogy mennyire bent lévõ pontok feleljenek meg, 0 és 1 közötti érték
        /// <returns></returns>
        public static List<Point> belsopontok(Point[] csucsok)
        {
            int xmax = Math.Max(csucsok[0].X, Math.Max(csucsok[1].X, csucsok[2].X));
            int xmin = Math.Min(csucsok[0].X, Math.Min(csucsok[1].X, csucsok[2].X));
            int ymax = Math.Max(csucsok[0].Y, Math.Max(csucsok[1].Y, csucsok[2].Y));
            int ymin = Math.Min(csucsok[0].Y, Math.Min(csucsok[1].Y, csucsok[2].Y));
            var kimenet = new List<Point>();
            var p1 = new PointF(csucsok[0].X, csucsok[0].Y);
            var p2 = new PointF(csucsok[1].X, csucsok[1].Y);
            var p3 = new PointF(csucsok[2].X, csucsok[2].Y);
            kimenet.Add(Point.Round(p1));
            kimenet.Add(Point.Round(p2));
            kimenet.Add(Point.Round(p3));
            for (int i = 0; i < xmax - xmin; i++) {
                for (int j = 0; j < ymax - ymin; j++) {
                    var p = new Point(i + xmin, j + ymin);
                    float alpha = ((p2.Y - p3.Y) * (p.X - p3.X) + (p3.X - p2.X) * (p.Y - p3.Y)) /
                        ((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));
                    float beta = ((p3.Y - p1.Y) * (p.X - p3.X) + (p1.X - p3.X) * (p.Y - p3.Y)) /
                        ((p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y));
                    if (alpha >= 0 && alpha < 1 && beta >= 0 && beta < 1 && (alpha + beta) <= 1)
                        kimenet.Add(Point.Round(p));
                }
            }

            return kimenet;

        }
    }
}
