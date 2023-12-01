## SolidWorks_ASsembly_Instructor (SWASI)
*Englische Version unten*

## 1. Beschreibung
Dieses Add-In für SolidWorks dient dazu, eine Baugruppe mit Informationen wie zum Beispiel Namen, Koordinatensysteme oder Unterbaugruppen in ein JSON-Format zu überführen, um sie später in eine Datenbank einpflegen zu können.

## 2. Voraussetzungen
Um das Tool nutzen zu können, müssen folgende Voraussetzungen gegeben sein:

1. Visual Studio 2022 (neuere Versionen sind wahrscheinlich kompatibel)

2. SolidWorks 2020 (neuere Versionen sind wahrscheinlich kompatibel)

3. GitHub-Konto

4. SolidWorks Add-In Installer ([GitHub](https://github.com/angelsix/solidworks-api/tree/develop/Tools/Addin%20Installer)) ([Alternative EXE in diesem Paket](https://angelsix.com/download/solidworks-files))

## 2. Kompilierung und Installation
### Kompilieren
Nach dem Öffnen des Projekts (SolidWorks_ASsembly_Instructor.sln) in VS22 kann der Build (Strg + B) ausgeführt werden. Das Ausführen über die "Start"-Schaltfläche wird nicht funktionieren, da eine DLL erstellt wird. Nach dem Buildvorgang befindet sich diese und zusätzliche DLLs im Bin-Ordner des Projekts. Die Kompilierung ist nur erfolgreich, wenn SolidWorks zuvor geschlossen wurde.

### Registrieren
Damit SolidWorks die erstellte DLL findet und das Add-In einbinden kann, muss diese in der Registry registriert werden. Dazu wird das Tool "SolidWorks Add-In Installer" verwendet. Zur ersten Nutzung muss auch hier eine Kompilation durchgeführt werden ([Anleitung](https://github.com/angelsix/solidworks-api/blob/develop/Tools/Addin%20Installer/README.md)).

![Oberfläche des Add-In Installers](./SolidWorks_ASsembly_Instructor/media/Window_SWAII.png)
Auf der linken Seite muss die RegAsm-Anwendung ausgewählt werden. Diese sollte standardmäßig eingetragen sein. Unter "Add-in Dll" muss der Pfad zur eigenen DLL (SolidWorks_ASsembly_Instructor.dll) ausgewählt werden. Durch einen Druck auf "Install" wird die DLL in SolidWorks registriert. Dieser Vorgang muss **nicht** ständig nach der Kompilierung wiederholt werden. Über "Previous add-in Dlls" können die eigenen DLLs beliebig deregistriert und neu registriert werden. Auf der rechten Seite befindet sich ein Überblick über alle gerade installierten Add-ins in SolidWorks.

### Verwenden
SolidWorks sollte die registrierte DLL nun in der "Taskpane" auf der rechten Seite am unteren Rand anzeigen. Sollte dies nicht der Fall sein, muss das Add-In zunächst aktiviert werden. Dazu klicke auf den Pfeil neben dem Zahnrad im Menüband, um das Menü für Zusatzanwendungen aufzurufen. Mit der Checkbox auf der linken Seite kann die Anwendung aktiviert werden. Die Checkbox auf der rechten Seite aktiviert das Laden beim Programmstart.

Zunächst muss unter "Save location" ein Speicherort für die JSON-Dateien ausgewählt werden. Nach der Betätigung des "Export as JSON"-Buttons werden mehrere JSON-Dateien an dem angegebenen Speicherort abgelegt. "Refresh part list" löscht die Ausgabe im Log-Fenster.

## 3. Debugging
Um die DLL zu debuggen, muss in Visual Studio unter "Debuggen" -> "An den Prozess anhängen" (Strg + Alt + P) an den Prozess "SLDWORKS.exe" angefügt werden. Nach dem Einstellen kann das erneute Anhängen mittels "Debuggen" -> "Erneut an Prozess anfügen" (Shift + Alt + P) erleichtert werden.

## 4. Struktur
| Ordner oder Datei | Beschreibung |
| ----------- | ----------- |
| TaskpaneHostUI.cs | Enthält sowohl die grafische Oberfläche als auch die Hauptfunktionalität (Wechseln mit (Shift) + F7) |
| TaskpaneIntegration.cs | Ermöglicht die Kommunikation mit SolidWorks (adaptiert von [hier](https://www.youtube.com/watch?v=7DlG6OQeJP0) mit Hilfe von [hier](https://stackoverflow.com/questions/74966397/making-c-sharp-class-library-com-visible-in-visual-studio-2022)) |
| Json/* | Enthält die JSON-Struktur als Klassen |
| AssemblyManager.cs | Haupt-Einstiegspunkt der Anwendung |
| utils/FeatureTypes.cs | Struct zur einfacheren Extraktion von Features |

Alles ist jetzt korrekt geschrieben.