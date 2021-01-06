using System;
using System.Linq;
using System.Collections.Generic;

namespace boboddyv2_api.Models
{
    class AcronymModel
    {
        public IDictionary<string, IDictionary<string, int>> WordF1 { get; set; }
        public IDictionary<string, IDictionary<string, int>> WordF2 { get; set; }
        public IDictionary<string, IDictionary<string, int>> PosF1 { get; set; }
        public IDictionary<string, IDictionary<string, int>> PosF2 { get; set; }
        public IDictionary<string, IDictionary<string, IEnumerable<string>>> PosWord { get; set; }

        public (
            IDictionary<string, IDictionary<string, int>>,
            IDictionary<string, IDictionary<string, int>>,
            IDictionary<string, IDictionary<string, int>>,
            IDictionary<string, IDictionary<string, int>>,
            IDictionary<string, IDictionary<string, IEnumerable<string>>>
        ) GetData()
        {

            return (WordF1, WordF2, PosF1, PosF2, PosWord);

        }
    }
}