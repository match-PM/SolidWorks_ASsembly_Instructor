using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonExporter_2
{
    internal class AssambleyManager
    {
        ModelDoc2 activeDoc;
        public AssambleyManager(SldWorks App) 
        {
            activeDoc = App.ActiveDoc as ModelDoc2;
            App.SendMsgToUser(activeDoc.ToString());
        }
    }
}
