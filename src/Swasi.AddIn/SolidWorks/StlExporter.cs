using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorks_ASsembly_Instructor
{
    internal sealed class StlExporter
    {
        private readonly SldWorks app;
        public StlExporter(SldWorks app) { this.app = app; }

        public void Export(ModelDoc2 document, string path, string originName)
        {
            int oneFile = (int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile;
            int binary = (int)swUserPreferenceToggle_e.swSTLBinaryFormat;
            int positive = (int)swUserPreferenceToggle_e.swSTLDontTranslateToPositive;
            int units = (int)swUserPreferenceIntegerValue_e.swExportStlUnits;
            int quality = (int)swUserPreferenceIntegerValue_e.swSTLQuality;
            int coordinateSystem = (int)swUserPreferenceStringValue_e.swFileSaveAsCoordinateSystem;
            bool oldOneFile = app.GetUserPreferenceToggle(oneFile);
            bool oldBinary = app.GetUserPreferenceToggle(binary);
            bool oldPositive = app.GetUserPreferenceToggle(positive);
            int oldUnits = app.GetUserPreferenceIntegerValue(units);
            int oldQuality = app.GetUserPreferenceIntegerValue(quality);
            string oldCoordinateSystem = document.GetUserPreferenceStringValue(coordinateSystem);
            // Stage output so a failed SaveAs cannot truncate an existing mesh.
            string temporaryPath = Path.Combine(Path.GetDirectoryName(path), Guid.NewGuid().ToString("N") + ".STL");
            try
            {
                app.SetUserPreferenceToggle(oneFile, true);
                app.SetUserPreferenceToggle(binary, true);
                app.SetUserPreferenceToggle(positive, true);
                app.SetUserPreferenceIntegerValue(units, (int)swLengthUnit_e.swMETER);
                app.SetUserPreferenceIntegerValue(quality, (int)swSTLQuality_e.swSTLQuality_Fine);
                string coordinateSystemName = string.IsNullOrEmpty(originName)
                    ? FeatureNameRules.OriginBase : FeatureNameRules.OriginPrefix + originName;
                if (!document.SetUserPreferenceStringValue(coordinateSystem, coordinateSystemName))
                    throw new InvalidOperationException("Could not select the STL export coordinate system.");
                int errors = 0, warnings = 0;
                bool saved = document.Extension.SaveAs3(temporaryPath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref errors, ref warnings);
                if (!saved || errors != 0 || !File.Exists(temporaryPath))
                    throw new IOException($"STL export failed (errors: {errors}, warnings: {warnings}).");
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                // Nested finally blocks ensure every preference restoration is attempted.
                try { document.SetUserPreferenceStringValue(coordinateSystem, oldCoordinateSystem); }
                finally
                {
                    try { app.SetUserPreferenceToggle(oneFile, oldOneFile); }
                    finally
                    {
                        try { app.SetUserPreferenceToggle(binary, oldBinary); }
                        finally
                        {
                            try { app.SetUserPreferenceToggle(positive, oldPositive); }
                            finally
                            {
                                try { app.SetUserPreferenceIntegerValue(units, oldUnits); }
                                finally
                                {
                                    try { app.SetUserPreferenceIntegerValue(quality, oldQuality); }
                                    finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
