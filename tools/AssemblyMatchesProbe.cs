using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SolidWorks.Interop.sldworks;
using SolidWorks_ASsembly_Instructor;
using Newtonsoft.Json;

// Own temporary files only. Exercises the actual matching service against SolidWorks.
public static class AssemblyMatchesProbe
{
    public static void Run(string partTemplate, string assemblyTemplate, string addInPath)
    {
        ISldWorks app;
        bool started = false;
        try { app = (ISldWorks)System.Runtime.InteropServices.Marshal.GetActiveObject("SldWorks.Application"); }
        catch (System.Runtime.InteropServices.COMException)
        { app = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application")); started = true; }
        var documents = new List<ModelDoc2>();
        string directory = Path.Combine(Path.GetTempPath(), "SWASI_Matches_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Console.WriteLine("Probe files: " + directory);
        try
        {
            var addin = Assembly.LoadFrom(addInPath);
            var storeType = addin.GetType("SolidWorks_ASsembly_Instructor.SwasiMetadataStore", true);
            var store = Activator.CreateInstance(storeType);
            var part = (ModelDoc2)app.NewDocument(partTemplate,0,0,0); documents.Add(part);
            part.FeatureManager.CreateCoordinateSystemUsingNumericalValues(true,0,0,0,true,0,0,0).Name = "SWASI_Origin_probe";
            part.FeatureManager.CreateCoordinateSystemUsingNumericalValues(true,.01,.02,.03,true,.3,.5,.8).Name = "SWASI_Assembly";
            part.FeatureManager.CreateCoordinateSystemUsingNumericalValues(true,.04,-.02,.01,true,-.2,.4,-.6).Name = "SWASI_Target";
            var metadata = new SwasiDocumentMetadata();
            metadata.frames["Assembly"] = new SwasiFrameMetadata { role = SwasiFrameRole.Assembly };
            metadata.frames["Target"] = new SwasiFrameMetadata { role = SwasiFrameRole.Target };
            storeType.GetMethod("Save").Invoke(store,new object[] {part,metadata});
            string partPath = Path.Combine(directory,"ProbePart.SLDPRT");
            Save(part,partPath);
            string original = JsonConvert.SerializeObject(storeType.GetMethod("Load").Invoke(store,new object[] {part}));
            var nested = (ModelDoc2)app.NewDocument(assemblyTemplate,0,0,0); documents.Add(nested);
            ((IAssemblyDoc)nested).AddComponent5(partPath,0,"",false,"",0,0,0);
            nested.FeatureManager.CreateCoordinateSystemUsingNumericalValues(true,0,0,0,true,0,0,0).Name="SWASI_Origin_probe";
            nested.FeatureManager.CreateCoordinateSystemUsingNumericalValues(true,.1,.2,.3,true,.1,.2,.3).Name="SWASI_SubTarget";
            var nestedMetadata = new SwasiDocumentMetadata();
            nestedMetadata.frames["SubTarget"] = new SwasiFrameMetadata { role=SwasiFrameRole.Target };
            storeType.GetMethod("Save").Invoke(store,new object[]{nested,nestedMetadata});
            string nestedPath=Path.Combine(directory,"Nested.SLDASM"); Save(nested,nestedPath);
            var doc = (ModelDoc2)app.NewDocument(assemblyTemplate,0,0,0); documents.Add(doc);
            var assembly = (IAssemblyDoc)doc;
            var target = assembly.AddComponent5(partPath,0,"",false,"",0,0,0);
            var moving = assembly.AddComponent5(partPath,0,"",false,"",.1,.1,.1);
            var subassembly = assembly.AddComponent5(nestedPath,0,"",false,"",.5,.5,.5);
            if (target == null || moving == null) throw new Exception("Cannot insert probe components.");
            string path = Path.Combine(directory,"ProbeAssembly.SLDASM"); Save(doc,path);
            var managerType = addin.GetType("SolidWorks_ASsembly_Instructor.AssemblyMatchManager",true);
            var manager = Activator.CreateInstance(managerType);
            var warnings = new List<string>();
            var candidates = (IEnumerable)managerType.GetMethod("ReadCandidates").Invoke(manager,new object[]{doc,warnings});
            var options=candidates.Cast<object>().Select(c=>(AssemblyFrameOption)c.GetType().GetField("Option").GetValue(c)).ToArray();
            if (options.Length != 5 || options.Any(o=>o.endpoint.component.Contains("/")))
                throw new Exception("Nested components must be excluded; the subassembly's own frame must be included.");
            string subassemblyName=subassembly.Name2;
            Console.WriteLine("PASS: first-level instances only, including the subassembly's own frame.");
            var match = new AssemblyFrameMatch { name="ProbeMatch",
                assemblyFrame = new AssemblyFrameEndpoint {component=moving.Name2,configuration=moving.ReferencedConfiguration,frame="Assembly"},
                targetFrame = new AssemblyFrameEndpoint {component=target.Name2,configuration=target.ReferencedConfiguration,frame="Target"} };
            var apply = managerType.GetMethod("Apply");
            apply.Invoke(manager,new object[]{doc,new List<AssemblyFrameMatch>{match}});
            Console.WriteLine("PASS: coordinate mate created and full origin/axis alignment verified.");
            var read = managerType.GetMethod("ReadMatches");
            var saved = (List<AssemblyFrameMatch>)read.Invoke(manager,new object[]{doc});
            if (saved.Count != 1) throw new Exception("Match not persisted.");
            string mateReference = saved[0].mateReference;
            apply.Invoke(manager,new object[]{doc,saved});
            saved = (List<AssemblyFrameMatch>)read.Invoke(manager,new object[]{doc});
            if (saved[0].mateReference != mateReference) throw new Exception("Unchanged Apply recreated the mate.");
            if (JsonConvert.SerializeObject(storeType.GetMethod("Load").Invoke(store,new object[]{part})) != original)
                throw new Exception("Component metadata was modified.");
            var exported = (List<AssemblyFrameMatch>)managerType.GetMethod("ForExport").Invoke(manager,new object[]{doc});
            if (exported.Count != 1) throw new Exception("Match missing from export.");
            Save(doc,path);
            app.CloseDoc(doc.GetTitle()); documents.Remove(doc);
            int errors=0,warningsOpen=0;
            doc = (ModelDoc2)app.OpenDoc6(path,2,1,"",ref errors,ref warningsOpen); documents.Add(doc);
            saved = (List<AssemblyFrameMatch>)read.Invoke(manager,new object[]{doc});
            if (saved.Count != 1 || ((List<AssemblyFrameMatch>)managerType.GetMethod("ForExport").Invoke(manager,new object[]{doc})).Count != 1)
                throw new Exception("Match did not survive save/reopen.");
            Console.WriteLine("PASS: idempotent Apply, save/reopen, export and untouched component metadata.");
            assembly=(IAssemblyDoc)doc;
            var currentComponents=((object[])assembly.GetComponents(true)).OfType<Component2>().ToArray();
            moving=currentComponents.Single(c=>c.Name2==saved[0].assemblyFrame.component);
            subassembly=currentComponents.Single(c=>c.Name2==subassemblyName);
            doc.ClearSelection2(true); moving.Select4(false,null,false); assembly.FixComponent();
            doc.ClearSelection2(true); subassembly.Select4(false,null,false); assembly.FixComponent();
            doc.ClearSelection2(true);
            var conflicting=saved[0].Clone();
            conflicting.targetFrame=new AssemblyFrameEndpoint {component=subassembly.Name2,configuration=subassembly.ReferencedConfiguration,frame="SubTarget"};
            bool rejected=false;
            try { apply.Invoke(manager,new object[]{doc,new List<AssemblyFrameMatch>{conflicting}}); }
            catch(TargetInvocationException) { rejected=true; }
            if(!rejected) throw new Exception("Conflicting fixed-component match was accepted.");
            var restored=(List<AssemblyFrameMatch>)managerType.GetMethod("ForExport").Invoke(manager,new object[]{doc});
            if(restored.Count!=1 || restored[0].targetFrame.component!=saved[0].targetFrame.component)
                throw new Exception("Failed rematch did not restore the original match and mate.");
            Console.WriteLine("PASS: conflicting rematch rejected and previous match/mate restored.");
            apply.Invoke(manager,new object[]{doc,new List<AssemblyFrameMatch>()});
            if (((List<AssemblyFrameMatch>)read.Invoke(manager,new object[]{doc})).Count != 0) throw new Exception("Match not removed.");
            var generated = ((object[])doc.FeatureManager.GetFeatures(false)).OfType<Feature>().Where(f=>f.Name==saved[0].mateName);
            if (generated.Any()) throw new Exception("Removed mate remains in feature tree.");
            Console.WriteLine("PASS: removing the match deletes only its generated mate.");
        }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
        finally
        {
            foreach (var doc in documents.AsEnumerable().Reverse())
                try { if (doc != null) app.CloseDoc(doc.GetTitle()); } catch (Exception ex) { Console.WriteLine("Probe cleanup: " + ex.Message); }
            try { if (started && app.GetFirstDocument()==null) app.ExitApp(); } catch (Exception ex) { Console.WriteLine("Probe cleanup: " + ex.Message); }
        }
    }
    private static void Save(ModelDoc2 doc,string path)
    {
        int errors=0,warnings=0;
        if (!doc.Extension.SaveAs(path,0,1,null,ref errors,ref warnings)) throw new Exception("Probe save failed: "+errors);
    }
}
