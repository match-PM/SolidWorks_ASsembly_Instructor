using System;
using SolidWorks.Interop.sldworks;

namespace SolidWorks_ASsembly_Instructor
{
    /// <summary>Restores the active document without closing any user documents.</summary>
    internal sealed class DocumentSession : IDisposable
    {
        private readonly SldWorks app;
        private readonly string originalTitle;
        private readonly Action<string, string> log;
        public DocumentSession(SldWorks app, ModelDoc2 original, Action<string, string> log)
        { this.app = app; originalTitle = original.GetTitle(); this.log = log; }
        public void Activate(ModelDoc2 document)
        {
            if (app.ActivateDoc2(document.GetTitle(), true, 0) == null)
                throw new InvalidOperationException($"Could not activate {document.GetTitle()}.");
        }
        public void Dispose()
        {
            try
            {
                if (app.ActivateDoc2(originalTitle, true, 0) == null)
                    log("Could not restore the originally active document.", "error");
            }
            catch (Exception ex) { log("Could not restore active document: " + ex.Message, "error"); }
        }
    }
}
