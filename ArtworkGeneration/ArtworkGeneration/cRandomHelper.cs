using System;

namespace ArtworkGeneration
{
	public class cRandomHelper
	{
		public int seed = 1;
		public System.Random rnd;
		public int lastRandomNumber;

		public cRandomHelper()	
		{
			rnd = new Random(seed);
		}
		public cRandomHelper(bool reseed)
		{
			if (reseed)
			{
				DateTime newDate = DateTime.Now;
				
				seed = newDate.GetHashCode(); 
				//	Convert.ToInt32(newDate.ToString("yy")) + newDate.GetHashCodeMMddHHmmssff")
				rnd = new Random(seed);
			}
			else
			{
				rnd = new Random(seed);
			}
		}
		public cRandomHelper(int newSeed)
		{
			seed = newSeed;
			rnd = new Random(seed);
		}

        public double RandomDouble(double from, double to)
        {
            return (rnd.NextDouble() * (to-from)) + from;
        }
        public int RandomNumber(int from, int to) 
		{
            lastRandomNumber = rnd.Next(from, to + 1); //Convert.ToInt32(Math.Floor((rnd.NextDouble() * (to-from+1-0.000001)) + (from)));
			return lastRandomNumber;
		}
        public int RandomSign()
        {
            return RandomNumber(0,1)==1 ? 1 : -1;
        }
        public int RandomNumberFromPcnt(double pcntLessThan1)
        {
            int rtn = 0;
            if (pcntLessThan1 > 0 && pcntLessThan1 < 1)
            {
                while (RandomDouble(0, 1) <= pcntLessThan1)
                { rtn += 1; }
            }
            return rtn;
        }
        public bool RollUnder(double under) 
		{
			
			return RollUnder((int) under, 100, 1);
		}
		public bool RollUnder(int under) 
		{
			return RollUnder(under, 100, 1);
		}
		public bool RollUnder(int under, int to) 
		{
			return RollUnder(under, to, 1);
		}
		public bool RollUnder(int under, int to, int from) 
		{
			// random number is equal or less than under
			lastRandomNumber = this.RandomNumber(from, to);
			if (lastRandomNumber <= under) return true;
			return false;
		}
		public int randomProbability(int from, int to) 
		{
			//3d4-2 => 1-10, avg 5.5
			int Dice2 = Convert.ToInt32(Math.Round((double)((to - from + 3) / 3), 0));
			int Dice1 = (to - from + 3) - (Dice2 * 2);
			int Roll3 = (from - 3) + (this.RandomNumber(1, Dice1) + this.RandomNumber(1, Dice2) + this.RandomNumber(1, Dice2));
			lastRandomNumber = Roll3;
			return lastRandomNumber;
		}
        public double GetDeviation(double averageDeviation)
        {
            return RandomDouble(-averageDeviation / 2d, averageDeviation / 2d);
        }
        public double RandomHeading()
        {
            return RandomDouble(0, Math.PI*2);
        }

    }
}
