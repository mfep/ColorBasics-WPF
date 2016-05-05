using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kolos
{
    public class pont3d
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

    public class normalvektor
    {
        public normalvektor(double a, double b, double c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }
        public double a;
        public double b;
        public double c;
        private double[] normalas(double x, double y, double z)
        {
            double hosssz = Math.Sqrt(x * x + y * y + z * z);
            var tomb = new double[3];
            tomb[0] = x / hosssz;
            tomb[1] = y / hosssz;
            tomb[2] = z / hosssz;
            return tomb;
        }
        public normalvektor kiszamitas(List<pont3d> lista)
        {
            double sumxx = 0;
            double sumyy = 0;
            double sumxy = 0;
            double sumxz = 0;
            double sumyz = 0;


            foreach (pont3d pont in lista) {
                if (sumxx == 0)
                    sumxx = pont.x * pont.x;
                else
                    sumxx += pont.x * pont.x;
            }
            foreach (pont3d pont in lista) {
                if (sumyy == 0)
                    sumyy = pont.y * pont.y;
                else
                    sumyy += pont.y * pont.y;
            }
            foreach (pont3d pont in lista) {
                if (sumxy == 0)
                    sumxy = pont.x * pont.y;
                else
                    sumxy += pont.x * pont.y;
            }
            foreach (pont3d pont in lista) {
                if (sumxz == 0)
                    sumxz = pont.x * pont.z;
                else
                    sumxz += pont.x * pont.z;
            }
            foreach (pont3d pont in lista) {
                if (sumyz == 0)
                    sumyz = pont.y * pont.z;
                else
                    sumyz += pont.y * pont.z;
            }
            double D = sumxx * sumyy - sumxy * sumxy;
            var normaltvektor = new double[3];
            normaltvektor = normalas((sumyz * sumxy - sumxz * sumyy) / D, (sumxy * sumxz - sumxx * sumyz) / D, 1);
            var megoldas = new normalvektor(normaltvektor[0], normaltvektor[1], normaltvektor[2]);
            return megoldas;

        }
    }
}
