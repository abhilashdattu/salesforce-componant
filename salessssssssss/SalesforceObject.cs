using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace salessssssssss
{
    public class SalesforceObject
    {
        public string Label { get; set; }
        public string ApiName { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string LastModified { get; set; }

        // Update to int
        public int ChildRelationshipsCount { get; set; }
        public int Fields { get; set; }
        public int ValidationResults { get; set; }
        public int RecordTypeInfos { get; set; }
    }


}
