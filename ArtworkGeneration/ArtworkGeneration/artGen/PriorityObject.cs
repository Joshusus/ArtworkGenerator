using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArtworkGeneration.artGen
{
    class PriorityObject
    {

        /** Generic class built by Josh Jones */

        public object target;
        public double score;
        public object extraObj;

        public PriorityObject(object target, double score)
        {
            this.target = target;
            this.score = score;
            extraObj = null;
        }

    }
}
