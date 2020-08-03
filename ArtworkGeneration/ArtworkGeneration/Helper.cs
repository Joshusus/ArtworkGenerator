using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;

namespace ArtworkGeneration
{
    partial class Helper
    {
        static int nextAssetID = 0;
        public static int nextAssetTokenID() { return nextAssetID++; }
        public static int nextObjectID = 2;
        public static int nextMessageID = 2;
        public static int nextUniqueSoundID = 0;
        public const double gravitationalConstant = 6.673e-11;
        public static float LightingAmbientPcnt = 0.3f;
        public static float LightingMax = 1.7f;
        public static double pcntRotationKeptInImpact = 0.9d;
        public static double DamageForceToVelocityRate = 0.1d;
        public static double ImpactForceToDamageRate = 0.1d * 0.71; //0.71 is to counter balances with gun speeds in blueprintgun
        public static double BulletImpactRate = 13d;
        public static double MassDivisor = 1000d; // convert Mass to length/force/damage/speed etc
        public static int HitPointsFromMass = 100;
        public const double PHI = 1.61803398874989484820458683436;
        public static double PHICalcLarge = 0.5 + Math.Sqrt(5) / 2; // golden ratio
        public static double PHICalcSmall = 0.5 - Math.Sqrt(5) / 2; // golden ratio

        //Map Generation
        public static int debug_MapGenSeed = DateTime.Now.Minute * DateTime.Now.Millisecond;

        //LINQ
        //public static bool CompareLists<T>(List<T> aListA, List<T> aListB)
        //{
        //    if (aListA == null || aListB == null || aListA.Count != aListB.Count)
        //        return false;
        //    if (aListA.Count == 0)
        //        return true;

        //    Dictionary<T, int> lookUp = new Dictionary<T, int>();
        //    // create index for the first list
        //    for (int i = 0; i < aListA.Count; i++)
        //    {
        //        int count = 0;
        //        if (!lookUp.TryGetValue(aListA[i], out count))
        //        {
        //            lookUp.Add(aListA[i], 1);
        //            continue;
        //        }
        //        lookUp[aListA[i]] = count + 1;
        //    }

        //    for (int i = 0; i < aListB.Count; i++)
        //    {
        //        int count = 0;
        //        if (!lookUp.TryGetValue(aListB[i], out count))
        //        {
        //            // early exit as the current value in B doesn't exist in the lookUp (and not in ListA)
        //            return false;
        //        }
        //        count--;
        //        if (count <= 0)
        //            lookUp.Remove(aListB[i]);
        //        else
        //            lookUp[aListB[i]] = count;
        //    }
        //    // if there are remaining elements in the lookUp, that means ListA contains elements that do not exist in ListB
        //    return lookUp.Count == 0;
        //}

        //Heat
        public static double TemperatureLostPerCycle = 0.01;
        public static double ConvertHeatToTemperature(double Heat, double Mass)
        {
            return Heat / (Mass*5);
        }

        
        //Rotation
        public static double GetRotationAmount(double currentDirection, double newDirection, double rotationPerCycle)
        {
            //TODO Rewrite to be more efficent
            double rotateAmount = 0;
            rotateAmount = Helper.HeadingDiff(currentDirection, newDirection);
            if (rotationPerCycle < Math.Abs(rotateAmount)) rotateAmount = (rotateAmount < 0) ? rotationPerCycle * -1 : rotationPerCycle;
            return rotateAmount;
        }
        public static float DegreesToRadians(float angle)
        {
            return angle * (float)Math.PI / 180;
        }

        //Points
        public static Point BlankPoint = new Point(0, 0);
        public static Point CircleCenter(Point A, Point B, Point C)
        {
            double yDelta_a = B.Y - A.Y;
            double xDelta_a = B.X - A.X;
            double yDelta_b = C.Y - B.Y;
            double xDelta_b = C.X - B.X;
            Point center = new Point(0, 0);
            double aSlope = yDelta_a / xDelta_a;
            double bSlope = yDelta_b / xDelta_b;
            center.X = (aSlope * bSlope * (A.Y - C.Y) + bSlope * (A.X + B.X) - aSlope * (B.X + C.X)) / (2 * (bSlope - aSlope));
            center.Y = -1 * (center.X - (A.X + B.X) / 2) / aSlope + (A.Y + B.Y) / 2;
            return center;
        }
        public static Point GetRelativePoint(Point p, double heading, double distance)
        {
            return GetRelativePoint(p.X, p.Y, heading, distance);
        }
        public static Point GetRelativePoint(Point centerPoint, Point targetPoint, double rotation)
        {
            var dist = Helper.GetABSDistance(centerPoint, targetPoint);
            var heading = GetHeadingTowardsPoint(centerPoint, targetPoint);
            heading = Helper.HeadingAdd(heading, rotation);
            return Helper.GetRelativePoint(centerPoint.X, centerPoint.Y, heading, dist);
        }
        public static Point GetXvYv(double heading, double speed)
        {
            return GetRelativePoint(0, 0, heading, speed);
        }
        public static Point GetRelativePoint(double x, double y, double heading, double distance)
        {
            if (distance < 0)
            {
                distance = Math.Abs(distance);
                heading = HeadingOpposite(heading);
            }
            return new Point(x + Math.Sin(heading) * distance, y - Math.Cos(heading) * distance);
        }
        public static Point CombinePoints(Point p1, Point p2)
        {
            /** Adds two points together. Does NOT average between the two */
            return new Point(p1.X + p2.X, p1.Y + p2.Y);
        }
        public static Point CombineForces(double heading1, double distance1, double heading2, double distance2)
        {
            return CombinePoints(GetRelativePoint(0, 0, heading1, distance1), GetRelativePoint(0, 0, heading2, distance2));
        }
        public static Point AveragePoints(List<Point> pts)
        {
            /** NOT the same as combining points. */
            double x = 0;
            double y = 0;
            foreach (Point pt in pts)
            {
                x += pt.X;
                y += pt.Y;
            }
            return new Windows.Foundation.Point(x / pts.Count, y / pts.Count);
        }
       

        //Velocity
        public static double RelativeApproachVelocity(double X, double Y, double Xv, double Yv, double X2, double Y2, double X2v, double Y2v)
        {
            //return GetDistance(o1.Xv - o2.Xv, o1.Yv - o2.Yv);
            double headingToTarget = GetHeadingTowardsPoint(X, Y, X2, Y2);
            double xbit = Math.Sin(headingToTarget) * (Xv - X2v);// * (o1.X > o2.X ? -1 : 1);
            double ybit = Math.Cos(headingToTarget) * (Yv - Y2v) * -1; //* (o1.Y > o2.Y ? -1 : 1);
            return xbit + ybit;
        }
        //Distance
        public static double GetABSDistance(Point p1, Point p2)
        {
            return Math.Abs(GetDistance(p1.X, p1.Y, p2.X, p2.Y));
        }
        public static double GetABSDistance(double x1, double y1, double x2, double y2)
        {
            return Math.Abs(GetDistance(x1, y1, x2, y2));
        }
        public static double GetDistance(int x1, int y1, int x2, int y2)
        {
            return GetDistance((double)x1 - x2, (double)y1 - y2);
        }
        public static double GetDistance(double x1, double y1, double x2, double y2)
        {
            return GetDistance(x1 - x2, y1 - y2);
        }
        public static double GetDistance(double x, double y) 
        { 
            return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2));
        }
        public static double GetDistance(Point o1, Point o2)
        {
            return Math.Sqrt(Math.Pow((o1.X - o2.X), 2) + Math.Pow((o1.Y - o2.Y), 2));
        }

        //Heading
        public static double PI22 = Math.PI / 8;
        public static double PI30 = Math.PI / 6;
        public static double PI45 = Math.PI / 4;
        public static double PI60 = Math.PI / 3;
        public static double PI90 = Math.PI / 2;
        public static double PI135 = Math.PI * 3 / 4;
        public static double PI180 = Math.PI;
        public static double PI225 = Math.PI * 5 / 4;
        public static double PI270 = Math.PI * 6 / 4;
        public static double PI315 = Math.PI * 7 / 4;
        public static double PI360 = Math.PI * 2;
        public static bool IsSameHeading(double heading1, double heading2)
        {
            return (Math.Round(heading1, 2) == Math.Round(heading2, 2) ||
                    Math.Round(heading1, 2) == Math.Round(heading2 + Helper.PI360, 2) ||
                    Math.Round(heading1, 2) == Math.Round(heading2 - Helper.PI360, 2));
        }
        public static bool IsSameHeading(double heading1, double heading2, double tolerance)
        {
            //heading1 *= 100;
            //heading2 *= 100;
            //tolerance *= 100;
            double diff = Math.Abs(heading1 - heading2);
            return (diff < tolerance) || (Math.Abs(diff - Helper.PI360) < tolerance);
        }
        public static double GetHeadingTowardsPoint(Point Position, Point TargetPosition)
        {
            return GetHeadingTowardsPoint(Position.X, Position.Y, TargetPosition.X, TargetPosition.Y);
        }
        public static double GetHeadingTowardsPoint(double xPosition, double yPosition, double xTargetPosition, double yTargetPosition)
        {
            double radians = Math.Atan2((xPosition - xTargetPosition), (yPosition - yTargetPosition));
            if (radians < 0) radians = radians * -1; else radians = (Math.PI - radians) + Math.PI;
            return radians;
        }
        public static int RadiansToDegrees(double radian)
        {
            // 2PI = 360 degrees            
            //return ((2 * Math.PI) * RotateDegrees) / 360; 
            return (int)(radian * 360f / (2f * Math.PI));
        }
        public static double HeadingOpposite(double heading)
        {
            return HeadingAdd(heading, Math.PI);
        }
        public static double HeadingAdd(double heading, double inc)
        {
            while (inc > PI360) { inc -= PI360; }
            var newheading = heading + inc;
            return newheading > PI360 ? newheading - PI360 : newheading < 0 ? newheading + Helper.PI360 : newheading;
        }
        public static bool HeadingClockwise(double h1, double h2)
        {
            if (h1 >= Math.PI)
                return !(h2 < h1 && h2 > h1 - Math.PI);
            else
                return (h2 > h1 && h2 < h1 + Math.PI);
        }        
        public static double HeadingDiff(double h1, double h2)
        {
            double diff = h2 - h1;
            if (diff > Math.PI) diff = diff - PI360;
            else if (diff < -Math.PI) diff = PI360 + diff;
            return diff;
        }
        public static double ReverseHeading(double CollisionHeading)
        {
            return CollisionHeading < Math.PI ? CollisionHeading + Math.PI : CollisionHeading - Math.PI;
        }
        public static double GetNewHeading(double currentHeading, double inc, double targetHeading)
        {
            if (currentHeading != targetHeading)
            {
                double diff = targetHeading - currentHeading;
                if (Math.Abs(diff) <= inc)
                {
                    return targetHeading;
                }
                else if (diff > 0 && diff <= Helper.PI180)
                {
                    return Helper.HeadingAdd(currentHeading, inc);
                }
                else if (diff < 0 && diff >= -Helper.PI180)
                {
                    return Helper.HeadingAdd(currentHeading, -inc);
                }
                else if (diff < -Helper.PI180)
                {
                    return Helper.HeadingAdd(currentHeading, inc);
                }
                else if (diff > Helper.PI180)
                {
                    return Helper.HeadingAdd(currentHeading, -inc);
                }
            }
            return targetHeading;
        }


        //Other
        public static double GetDirection(double xv, double yv)
        {
            return GetHeadingTowardsPoint(0, 0, xv, yv);
        }
        public static double UpdateVariable(double variable, ref double decrease, double defaultDecrease, double min, double max)
        {
            //decrease variable by from max to 0 by decrease, if decrease is 0 and variable is below min decrease set to defaultDecrease
            if (variable > max) variable = max; else if (variable < -max) variable = -max;
            if ((decrease > 0 && variable < 0) || (decrease < 0 && variable > 0)) decrease = -decrease;

            if (variable != 0)
            {
                if (variable >= -decrease && variable <= decrease)
                {
                    variable = 0;
                }
                else
                {
                    if (decrease == 0)
                    {
                        if (variable > -min && variable < 0) decrease = -defaultDecrease;
                        else if (variable < min && variable > 0) decrease = defaultDecrease;
                    }
                    variable -= decrease;
                }
            }
            return variable;
        }        
        public static int CalcCycles(double velocity,double range)
        {
            return (int)((range * 60d) / velocity);
        }
        public static int GetSign(double value)
        {
            return value >= 0 ? 1 : -1;
        }
        public static bool InRectangle(double X, double Y, double Xmin, double Ymin, double Xmax, double Ymax)
        {
            return X >= Xmin && X <= Xmax && Y >= Ymin && Y <= Ymax;
        }
        public static string Spaces(int num)
        {
            StringBuilder s = new StringBuilder();
            for (var i=1; i<=num; i++)
            {
                s.Append(" ");
            }
            return s.ToString();
        }
        public static void AdjustPcnt0to1(ref double Pcnt, double adjust)
        {
            if (adjust != 0) Pcnt += adjust;
            if (Pcnt > 1) Pcnt = 1;
            else if (Pcnt < 0) Pcnt = 0;
        }
        public static bool PointBetweenPoints(Point p, Point p1, Point p2)
        {
            if (p.X < (p1.X < p2.X ? p1.X : p2.X) || p.X > (p1.X > p2.X ? p1.X : p2.X)) return false;
            if (p.Y < (p1.Y < p2.Y ? p1.Y : p2.Y) || p.Y > (p1.Y > p2.Y ? p1.Y : p2.Y)) return false;
            return true;
        }
        public static double GetUpdatedValue(double current, double absInc, double target)
        {
            if (current != target)
            {
                double diff = target - current;
                if (Math.Abs(diff) <= absInc)
                {
                    return target;
                }
                else if (diff > 0 )
                {
                    return current + absInc;
                }
                else
                {
                    return current - absInc;
                }
            }
            return target;
        }

        //Color
        public static float Brightness(Windows.UI.Color clr)
        {
            return (((float)clr.R) + ((float)clr.G) + ((float)clr.B)) / 765;
        }
        public static Color CombineColors(Color c1, Color c2, float pcntC1)
        {
            float pcntC2 = 1 - pcntC1;
            var c = new Color();
            c.R = (byte)(((((float)c1.R) / 255f) * pcntC1 + (((float)c2.R) / 255f) * pcntC2) * 255);
            c.G = (byte)(((((float)c1.G) / 255f) * pcntC1 + (((float)c2.G) / 255f) * pcntC2) * 255);
            c.B = (byte)(((((float)c1.B) / 255f) * pcntC1 + (((float)c2.B) / 255f) * pcntC2) * 255);
            c.A = (byte)(((((float)c1.A) / 255f) * pcntC1 + (((float)c2.A) / 255f) * pcntC2) * 255);
            return c;
        }
        public static Color ColorByVal(Color c)
        {
            return Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        public static void ChangeColorBrightness(ref Color changeColor, float pcntChange)
        {
            changeColor.R = (byte)(((float)changeColor.R) * pcntChange);
            changeColor.G = (byte)(((float)changeColor.G) * pcntChange);
            changeColor.B = (byte)(((float)changeColor.B) * pcntChange);
        }
        public static Matrix3x2 GetDisplayTransform(Vector2 outputSize, Vector2 sourceSize)
        {
            // Scale the display to fill the control.
            var scale = outputSize / sourceSize;
            var offset = Vector2.Zero;

            // Letterbox or pillarbox to preserve aspect ratio.
            if (scale.X > scale.Y)
            {
                scale.X = scale.Y;
                offset.X = (outputSize.X - sourceSize.X * scale.X) / 2;
            }
            else
            {
                scale.Y = scale.X;
                offset.Y = (outputSize.Y - sourceSize.Y * scale.Y) / 2;
            }

            return Matrix3x2.CreateScale(scale) *
                   Matrix3x2.CreateTranslation(offset);
        }
        //public static void ChangeColorToColor(ref Color changeColor, Color ToColor)
        //{
        //    changeColor.A = ToColor.A;
        //    changeColor.R = ToColor.R;
        //    changeColor.G = ToColor.G;
        //    changeColor.B = ToColor.B;
        //}

        //public static double CombinedVelocity(double o1Xv, double o1Yv, double o2Xv, double o2Yv)
        //{
        //    return Math.Sqrt(Math.Pow((o1Xv + o2Xv), 2) + Math.Pow((o1Yv + o2Yv), 2)); 
        //}

        public static Color Silver2Yellow2Red(double pcnt)
        {
            // White -  Yellow -    Orange - Red
            // 0        0.25        0.65        1 
            //          rgb(255,255,0)       rgb(255,0,0)
            //                      rgb(255,165,0)
            // 0 - 0.25 RG=255 B255 => 0
            // 0.25 - 1 R=255 B0 G=255 - 0
            byte R = 192;
            byte G = 192;
            byte B = 192;
            double yellowfrom = 0.2;
            if (pcnt <= 0) { }
            else if (pcnt >= 1)
            {
                G = 0; B = 0;
            }
            else if (pcnt <= yellowfrom)
            {
                B = (byte)(192 * ((yellowfrom - pcnt) / yellowfrom));
            }
            else
            {
                B = 0;
                G = (byte)(192 * (1 - pcnt) / (1 - yellowfrom));
            }
            return Color.FromArgb(192, R, G, B);
        }

        public static Color White2Yellow2Red(double pcnt)
        {
            // White -  Yellow -    Orange - Red
            // 0        0.25        0.65        1 
            //          rgb(255,255,0)       rgb(255,0,0)
            //                      rgb(255,165,0)
            // 0 - 0.25 RG=255 B255 => 0
            // 0.25 - 1 R=255 B0 G=255 - 0
            byte R = 255;
            byte G = 255;
            byte B = 255;
            double yellowfrom = 0.2;
            if (pcnt <= 0) { }
            else if (pcnt >= 1)
            {
                G = 0; B = 0;
            }
            else if (pcnt <= yellowfrom)
            {
                B = (byte)(255 * ((yellowfrom - pcnt) / yellowfrom));
            }
            else
            {
                B = 0;
                G = (byte)(255 * (1 - pcnt) / (1 - yellowfrom));
            }
            return Color.FromArgb(255, R, G, B);
        }
        public static Color Blue2Red(double pcnt)
        {
            byte R = (byte)(255*pcnt);
            byte B = (byte)(255*(1-pcnt));
            return Color.FromArgb(255, R, 0, B);
        }
    }
}
