// system 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

// ODA
using Teigha.Runtime;
using Teigha.DatabaseServices;
using Teigha.Geometry;

// Bricsys
using Bricscad.ApplicationServices;
using Bricscad.Runtime;
using Bricscad.EditorInput;
using Bricscad.Ribbon;
using Bricscad.Geometrical3dConstraints;


// com
using BricscadDb;
using BricscadApp;

// alias
using _AcRx = Teigha.Runtime;
using _AcAp = Bricscad.ApplicationServices;
using _AcDb = Teigha.DatabaseServices;
using _AcGe = Teigha.Geometry;
using _AcEd = Bricscad.EditorInput;
using _AcGi = Teigha.GraphicsInterface;
using _AcClr = Teigha.Colors;
using _AcWnd = Bricscad.Windows;

using CadCom;
using CadCom.Entity;
// this attribute marks this class, as a class that contains commands or lisp callable functions 
[assembly: CommandClass(typeof(ManagementPosi.Commands))]


// this attribute marks this class, as a class having ExtensionApplication methods
// Initialize and Terminate that are called on loading and unloading of this assembly 
[assembly: ExtensionApplication(typeof(ManagementPosi.Commands))]
namespace ManagementPosi
{

    ///<summary>
    ///機能説明：<CADアドオン開発するためのサンプルクラス>
    ///作成者：高
    ///作成日：<2022/11/01>
    ///</summary>
    public class Commands
    {

        ///<summary>
        ///GroupObjectからデータを読み込む
        ///</summary>
        [CommandMethod("GetDataFrGroup")]
        public void GetDataFrGroup()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                DBDictionary groupdic = tr.GetObject(db.GroupDictionaryId, OpenMode.ForWrite) as DBDictionary;
                Editor ed = doc.Editor;

                ObjectId groupid = groupdic.GetAt("GGGROUP");

                Pipe p = groupid.GetObject<Group>().FetchDataFrEntity<Pipe>("manageposi");
                tr.Commit();
            }
        }

        ///<summary>
        ///GroupObjectにデータを書き込む
        ///</summary>
        [CommandMethod("AddData2Group")]
        public void AddData2Group()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                DBDictionary groupdic = tr.GetObject(db.GroupDictionaryId, OpenMode.ForWrite) as DBDictionary;
                Editor ed = doc.Editor;

                ObjectId groupid = groupdic.GetAt("GGGROUP");

                Pipe pipe = new Pipe();
                pipe.ConnectedPipeIdOne = "p001";
                pipe.ConnectedPipeXCoordinateOrg = 1234.56789;

                groupid.GetObject<Group>().AttachData2Entity(pipe, "manageposi");
                tr.Commit();
            }
        }

        ///<summary>
        ///blockを作成して、該当blockを参照しBlockReferenceを作成する
        ///新しい方法
        ///</summary>
        [CommandMethod("InsertingABlock")]
        public void InsertingABlock() 
        {
            Database db;
            db = Application.DocumentManager.MdiActiveDocument.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction()) 
            {
                db.PlotManagePosi("CompletedManagePosi",new Point3d(40000,40000,0));
                db.PlotManagePosi("CompletedManagePosi", new Point3d(41000, 40000, 0));
                db.PlotManagePosi("CompletedManagePosi", new Point3d(42000, 40000, 0));
                tr.Commit();
            }
        }

        ///<summary>
        ///blockを作成して、該当blockを参照しBlockReferenceを作成する
        ///古い方法
        ///</summary>
        [CommandMethod("InsertingABlockOld")]
        public void InsertingABlockOld()
        {
            // Get the current database and start a transaction
            Database acCurDb;
            acCurDb = Application.DocumentManager.MdiActiveDocument.Database;

            using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
            {
                // Open the Block table for read
                BlockTable acBlkTbl;
                acBlkTbl = acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                ObjectId blkRecId = ObjectId.Null;

                if (!acBlkTbl.Has("ManagePosi"))
                {
                    using (BlockTableRecord acBlkTblRec = new BlockTableRecord())
                    {
                        acBlkTblRec.Name = "ManagePosi";

                        // Set the insertion point for the block
                        acBlkTblRec.Origin = new Point3d(0, 0, 0);

                        // Add a circle to the block
                        using (Circle acCirc = new Circle())
                        {
                            acCirc.Center = new Point3d(0, 0, 0);
                            acCirc.Radius = 500;

                            acBlkTblRec.AppendEntity(acCirc);

                            acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForWrite);
                            acBlkTbl.Add(acBlkTblRec);
                            acTrans.AddNewlyCreatedDBObject(acBlkTblRec, true);
                        }

                        blkRecId = acBlkTblRec.Id;
                    }
                }
                else
                {
                    blkRecId = acBlkTbl["ManagePosi"];
                }

                // Insert the block into the current space
                if (blkRecId != ObjectId.Null)
                {
                    using (BlockReference acBlkRef = new BlockReference(new Point3d(50000, 50000, 0), blkRecId))
                    {
                        BlockTableRecord acCurSpaceBlkTblRec;
                        acCurSpaceBlkTblRec = acTrans.GetObject(acCurDb.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                        acCurSpaceBlkTblRec.AppendEntity(acBlkRef);
                        acTrans.AddNewlyCreatedDBObject(acBlkRef, true);
                    }
                }

                // Save the new object to the database
                acTrans.Commit();

                // Dispose of the transaction
            }
        }

        ///<summary>
        ///管リストを図面の管要素に書き込む
        ///新しい方法
        ///</summary>
        [CommandMethod("MappingPipes")]
        public static void MappingPipes()
        {
            // APIでPipeｓデータを取る
            string path = @".\Entity\Dummy_Api_AllPipes.json";
            string json = CadCommon.ReadJsonFrLocal(path);
            
            List<Pipe> pipesOfApi = JsonParser.Json2entity<List<Pipe>>(json);
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (Transaction tr = db.TransactionManager.StartTransaction()) 
            {
                db.MappingPipes(pipesOfApi);
                tr.Commit();
            }
        }

        ///<summary>
        ///図面の管要素に書き込むデータを取って出力する
        ///</summary>
        [CommandMethod("CheckPipeData")]
        public static void CheckPipeData() 
        {
            string path = "C:\\Temp\\corddata.txt";
            int pipeCount = 0;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            StreamWriter file = new StreamWriter(path, true);
            List<ObjectId> objectids = CadCommon.FilterBlockReference();
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objectid in objectids)
                {
                    Entity ent = tr.GetObject(objectid, OpenMode.ForRead) as Entity;
                    Pipe p = ent.FetchDataFrEntity<Pipe>();
                    if (p != null) 
                    {
                        //ed.WriteMessage("ori X:" + ent.CompoundObjectTransform.CoordinateSystem3d.Origin.X.ToString());
                        //ed.WriteMessage("ori Y:" + ent.CompoundObjectTransform.CoordinateSystem3d.Origin.Y.ToString());
                        //ed.WriteMessage("PipeOld prop X:"+ p.ConnectedPipeXCoordinateOrg.ToString());
                        //ed.WriteMessage("PipeOld prop Y:" + p.ConnectedPipeYCoordinateOrg.ToString());
                        file.WriteLine("ori X:" + ent.CompoundObjectTransform.CoordinateSystem3d.Origin.X.ToString());
                        file.WriteLine("ori Y:" + ent.CompoundObjectTransform.CoordinateSystem3d.Origin.Y.ToString());
                        file.WriteLine("PipeOld prop X:" + p.ConnectedPipeXCoordinateOrg.ToString());
                        file.WriteLine("PipeOld prop Y:" + p.ConnectedPipeYCoordinateOrg.ToString());
                        pipeCount++;
                    }
                }
                file.Close();
            }
            ed.WriteMessage(pipeCount.ToString());
        }

        ///<summary>
        ///管リストを図面の管要素に書き込む
        ///古い方法
        ///</summary>
        [CommandMethod("MappingPipesOld")]
        public static void MappingPipesOld() 
        {

            // APIでPipeｓデータを取る
            //string pathtemp = System.Environment.CurrentDirectory;
            string path = @".\Entity\Dummy_Api_AllPipes.json";
            string json = CadCommon.ReadJsonFrLocal(path);
            List<Pipe> pipesOfApi = JsonParser.Json2entity<List<Pipe>>(json);

            int pipeCount = 0;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            List<ObjectId> objectids = CadCommon.FilterBlockReference();
            using (Transaction tr = db.TransactionManager.StartTransaction()) 
            {
                foreach (Pipe apiP in pipesOfApi) 
                {
                    foreach (ObjectId objectid in objectids) 
                    {
                        Entity ent = tr.GetObject(objectid, OpenMode.ForRead) as Entity;
                        if (apiP.ConnectedPipeXCoordinateOrg.Equals(Math.Round(ent.CompoundObjectTransform.CoordinateSystem3d.Origin.X,6,MidpointRounding.AwayFromZero)) && 
                            apiP.ConnectedPipeYCoordinateOrg.Equals(Math.Round(ent.CompoundObjectTransform.CoordinateSystem3d.Origin.Y,6, MidpointRounding.AwayFromZero)))
                        {
                            apiP.Objectid = objectid.ToString();
                            ent.AttachData2Entity<Pipe>(apiP);
                            pipeCount++;
                        }
                    }
                }

                tr.Commit();
            }
            ed.WriteMessage(pipeCount.ToString());

        }

        ///<summary>
        ///C#のObjectからbase64のstringに変換し図面要素のXDataにほぞんする
        ///</summary>
        [CommandMethod("AddXdata2Entity")]
        static public void AddXdata2Entity()
        {
            Logger.Log(Logger.Level_Info, "method Begin");
            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = HostApplicationServices.WorkingDatabase;
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;

            List<PipeOld> pipes = (List<PipeOld>)DummyDataGenerator.GeneratorManyPipes();
            //string pipesjson = Request.Post(Request.BaseUrl, (string)JsonParser.Entity2Json(pipes));
            //List<PipeOld> pipesentity = JsonParser.Json2entity<List<PipeOld>>(pipesjson);
            byte[] bytes = JsonParser.Serialize(pipes);
            string base64entity = JsonParser.ToBase64String(bytes);

            PromptEntityResult per = editor.GetEntity("blockをとってください。");

            string objectId = "";
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockReference br = (BlockReference)per.ObjectId.GetObject(OpenMode.ForWrite);
                CadCommon.AddData2Entity(db, tr, br.ObjectId, base64entity);
                objectId = br.ObjectId.ToString();
            }


            // CadCommon.Log2Editor(objectId);
            Logger.Log(Logger.Level_Console, "log to Console for test");
            Logger.Log(Logger.Level_Info, "method End");
        }

        ///<summary>
        ///図面のModelSpaceにある各Blockreferenceの座標をを取ってプリンターする
        ///</summary>
        [CommandMethod("PrintCoordinateSystem3dOfAllBlockRef")]
        public void PrintCoordinateSystem3dOfAllBlockRef()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            List<ObjectId> objectids = new List<ObjectId>();
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                ed.WriteMessage("\n座標をリストする：");
                foreach (ObjectId item in btr)
                {
                    Entity ent = trans.GetObject(item, OpenMode.ForRead) as Entity;

                    if (ent.GetType() == typeof(BlockReference) && ent != null && ent.CompoundObjectTransform != null && ent.CompoundObjectTransform.CoordinateSystem3d != null &&
                       ent.CompoundObjectTransform.CoordinateSystem3d.Origin != null)
                    {
                        objectids.Add(ent.ObjectId);
                        ed.WriteMessage(" X:" + ent.CompoundObjectTransform.CoordinateSystem3d.Origin.X.ToString());
                        ed.WriteMessage(" Y:" + ent.CompoundObjectTransform.CoordinateSystem3d.Origin.Y.ToString());
                        ed.WriteMessage("\n");
                        CadCommon.FetchAllXDataFrEntity(ed, ent.ObjectId);
                        ed.WriteMessage("\n");
                    }
                }

                ed.WriteMessage(objectids.Count.ToString());
            }
        }

        ///<summary>
        ///図面にある各BlockTableRecordを取る
        ///</summary>
        [CommandMethod("GetAllBlock")]
        static public void GetAllBlock() 
        {
            List<string> blocknames = new List<string>();
            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = HostApplicationServices.WorkingDatabase;
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
            
            using (Transaction tr = db.TransactionManager.StartTransaction()) 
            {
                BlockTable bt = db.BlockTableId.GetObject(OpenMode.ForRead) as BlockTable;
                foreach (var item in bt)
                {
                    BlockTableRecord btr = (BlockTableRecord)item.GetObject(OpenMode.ForRead);
                    if (btr != null)
                    {
                        doc.Editor.WriteMessage(bt.Has(btr.Name).ToString());
                        doc.Editor.WriteMessage(btr.Name);
                        doc.Editor.WriteMessage("\n");
                        blocknames.Add(btr.Name);
                    }
                }
                tr.Commit();
                doc.Editor.WriteMessage(blocknames.Count.ToString());
            }
        }

        ///<summary>
        ///Objectを図面から削除する
        ///</summary>
        [CommandMethod("EraseObject")]
        static public void EraseObject() 
        {
            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = HostApplicationServices.WorkingDatabase;
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
            PromptEntityResult per = editor.GetEntity("block");
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockReference br = (BlockReference)per.ObjectId.GetObject(OpenMode.ForWrite);
                br.Erase(true);

                tr.Commit();

            }
        }

        ///<summary>
        ///C#のObjectからbase64のstringに変換し図面要素のXDataにほぞんする
        ///</summary>
        [CommandMethod("GetBlockByRef")]
        static public void GetBlockByRef()
        {

            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = HostApplicationServices.WorkingDatabase;
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
            PromptEntityResult per = editor.GetEntity("blockをとってください。");
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockReference br = (BlockReference)per.ObjectId.GetObject(OpenMode.ForRead);
                BlockReference brclone = (BlockReference)br.Clone();
                BlockTable bt = db.BlockTableId.GetObject(OpenMode.ForRead) as BlockTable;

                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[br.Name], OpenMode.ForWrite, false) as BlockTableRecord;

                foreach (ObjectId entityId in btr) 
                {
                    DBObject ent = tr.GetObject(entityId, OpenMode.ForWrite, false) as DBObject;
                }

                // DBDictionary edic = (DBDictionary)btr.ExtensionDictionary.GetObject(OpenMode.ForWrite);

                CadCommon.readAttrLoop(doc, db, tr,  btr.ExtensionDictionary,"");
            }
        }

        ///<summary>
        ///Filterで図面にある要素を検索する
        ///</summary>
        [CommandMethod("SelectionFilter")]
        static public void SelectionFilter() 
        {

            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = HostApplicationServices.WorkingDatabase;
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
            TypedValue[] typevalues = new TypedValue[1];
            // typevalues.SetValue(new TypedValue((int)DxfCode.Start, "Circle"),0);
            // typevalues.SetValue(new TypedValue((int)DxfCode.UcsOrientationX, 63533.066447), 0);
            typevalues.SetValue(new TypedValue((int)DxfCode.BlockName, "CompletedManagePosi"), 0);

            SelectionFilter filter = new SelectionFilter(typevalues);
            PromptSelectionResult psr = editor.SelectAll(filter);
            SelectionSet ss =  psr.Value;
            using (Transaction tr = db.TransactionManager.StartTransaction()) 
            {
                foreach (ObjectId objectid in ss.GetObjectIds())
                {
                    Entity ent = objectid.GetObject<Entity>();
                }

                tr.Commit();
            }
        }

        ///<summary>
        ///C#のObjectからbase64のstringに変換し図面要素のXDataにほぞんする
        ///</summary>
        [CommandMethod("ReadBlock")]
        static public void ReadBlock() 
        {
            Logger.Log(Logger.Level_Info, "method Begin");
            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = HostApplicationServices.WorkingDatabase;
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;


            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = db.BlockTableId.GetObject(OpenMode.ForRead) as BlockTable;

                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false) as BlockTableRecord;

                foreach (var item in bt)
                {
                    //BlockTableRecord btr = (BlockTableRecord)item.GetObject(OpenMode.ForRead);
                    if (btr != null) 
                    {
                        //btr.ObjectId.GetObject();

                    }
                    doc.Editor.WriteMessage(bt.Has(btr.Name).ToString());
                    doc.Editor.WriteMessage(btr.Name);
                    doc.Editor.WriteMessage("\n");
                }

                foreach (ObjectId entityId in btr) 
                {
                    BlockReference br = tr.GetObject(entityId, OpenMode.ForWrite, false) as BlockReference;
                    Entity ent = tr.GetObject(entityId, OpenMode.ForWrite, false) as Entity;
                
                    // ent.
                    if (br != null && br.CompoundObjectTransform != null && br.CompoundObjectTransform.CoordinateSystem3d != null &&
                        br.CompoundObjectTransform.CoordinateSystem3d.Origin != null) 
                    {
                        editor.WriteMessage(" X:"+ br.CompoundObjectTransform.CoordinateSystem3d.Origin.X.ToString());
                        editor.WriteMessage(" Y:" + br.CompoundObjectTransform.CoordinateSystem3d.Origin.Y.ToString());
                        editor.WriteMessage("\n");
                    }
                 }
            }
        }


        ///<summary>
        ///C#のObjectからbase64のstringに変換し図面要素のXDataにほぞんする
        ///</summary>
        [CommandMethod("add1Object2Entity")]
        static public void Add1Object2Entity()
        {
            Logger.Log(Logger.Level_Info,"method Begin");
            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = HostApplicationServices.WorkingDatabase;
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;

            List<PipeOld> pipes = (List<PipeOld>)DummyDataGenerator.GeneratorManyPipes();
            string pipesjson = Request.Post(Request.BaseUrl,(string)JsonParser.Entity2Json(pipes));
            List<PipeOld> pipesentity = JsonParser.Json2entity<List<PipeOld>>(pipesjson);
            byte[] bytes = JsonParser.Serialize(pipesentity);
            string base64entity = JsonParser.ToBase64String(bytes);

            PromptEntityResult per = editor.GetEntity("blockをとってください。");

            string objectId = "";
            using (Transaction tr = db.TransactionManager.StartTransaction()) 
            {
                BlockReference br = (BlockReference)per.ObjectId.GetObject(OpenMode.ForWrite);
                CadCommon.AddData2Entity(db, tr, br.ObjectId, base64entity);
                objectId = br.ObjectId.ToString();
            }


            // CadCommon.Log2Editor(objectId);
            Logger.Log(Logger.Level_Console, "log to Console for test");
            Logger.Log(Logger.Level_Info, "method End");
        }

        ///<summary>
        ///図面要素のXDataから、データを取得してC#のObjectへ変換する
        ///</summary> 
        [CommandMethod("get1ObjectFrEntity")]
        static public void Get1ObjectFrEntity()
        {
            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = HostApplicationServices.WorkingDatabase;
            Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;

            PromptEntityResult per = editor.GetEntity("blockをとってください。");
            using (Transaction tr = db.TransactionManager.StartTransaction()) 
            {
                BlockReference br = (BlockReference)per.ObjectId.GetObject(OpenMode.ForRead);
                
                string base64Entity = CadCommon.FetchDataFrEntity(br.ObjectId);
                byte[] bytes = JsonParser.FromBase64String(base64Entity);
                Object obj = JsonParser.Deserialize(bytes);
                List<PipeOld> pipes = (List<PipeOld>)obj;
                CadCommon.Log2Editor(br.ObjectId.ToString());

                foreach (PipeOld p in pipes) {
                    CadCommon.Log2Editor(p.Pipeid.ToString());
                    CadCommon.Log2Editor(p.Pipename.ToString());
                   //CadCommon.Log2Editor(p..ToString());
                }
                
            }
        }

        // This attribute marks this function as being command line callable 
        [CommandMethod("Log2Editor")]
        static public void Log2Editor()
        {
            //Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            //Database db = HostApplicationServices.WorkingDatabase;
            //Editor editor = Application.DocumentManager.MdiActiveDocument.Editor;
            //doc.Editor.WriteMessage("Test for editor");

            CadCommon.Log2Editor("テストログ");
        }

        ///<summary>
        ///図面要素のXDataにデータを追加する
        ///</summary> 
        [CommandMethod("AttachXDataToSelectionSetObjects")]
        public void AttachXDataToSelectionSetObjects()
        {
            // Get the current database and start a transaction
            Database acCurDb;
            acCurDb = Application.DocumentManager.MdiActiveDocument.Database;

            Document acDoc = Application.DocumentManager.MdiActiveDocument;

            string appName = "MY_APP";
            string xdataStr = "This is some xdata";

            using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
            {
                // Request objects to be selected in the drawing area
                PromptSelectionResult acSSPrompt = acDoc.Editor.GetSelection();

                // If the prompt status is OK, objects were selected
                if (acSSPrompt.Status == PromptStatus.OK)
                {
                    // Open the Registered Applications table for read
                    RegAppTable acRegAppTbl;
                    acRegAppTbl = acTrans.GetObject(acCurDb.RegAppTableId, OpenMode.ForRead) as RegAppTable;

                    // Check to see if the Registered Applications table record for the custom app exists
                    if (acRegAppTbl.Has(appName) == false)
                    {
                        using (RegAppTableRecord acRegAppTblRec = new RegAppTableRecord())
                        {
                            acRegAppTblRec.Name = appName;

                            acTrans.GetObject(acCurDb.RegAppTableId, OpenMode.ForWrite);
                            acRegAppTbl.Add(acRegAppTblRec);
                            acTrans.AddNewlyCreatedDBObject(acRegAppTblRec, true);
                        }
                    }

                    // Define the Xdata to add to each selected object
                    using (ResultBuffer rb = new ResultBuffer())
                    {
                        rb.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName, appName));
                        rb.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, xdataStr));

                        SelectionSet acSSet = acSSPrompt.Value;

                        // Step through the objects in the selection set
                        foreach (SelectedObject acSSObj in acSSet)
                        {
                            // Open the selected object for write
                            Entity acEnt = acTrans.GetObject(acSSObj.ObjectId,
                                                                OpenMode.ForWrite) as Entity;

                            // Append the extended data to each object
                            acEnt.XData = rb;
                        }
                    }
                }

                // Save the new object to the database
                acTrans.Commit();

                // Dispose of the transaction
            }
        }

        // This attribute marks this function as being command line callable 
        [CommandMethod("WriteMoreAndMore")]
        static public void Write500PipesList()
        {
            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            _AcEd.Editor editor = _AcAp.Application.DocumentManager.MdiActiveDocument.Editor;
            _AcEd.PromptStringOptions pop = new _AcEd.PromptStringOptions("this is a test");
            _AcEd.PromptResult res = editor.GetString(pop);
            string userinput = res.StringResult;
            List<PipeOld> pList = new List<PipeOld>();
            for (int i = 0; i < 5000; i++)
            {
                PipeOld p = new PipeOld();
                p.Pipeid = 1003 + i;
                p.Pipename = "P003N" + userinput + i.ToString();
                p.Poixy = new string[3] { "高", "Joanne", "Robert" };
                pList.Add(p);
            }
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                DBDictionary nod = tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite) as DBDictionary;

                // nod.SetDataToDBdictionary<List<PipeOld>>("pipes1", pList);
                nod.SetListToDBdictionary<PipeOld>("pipes1", pList);

                tr.Commit();
            }
        }

        // This attribute marks this function as being command line callable 
        [CommandMethod("GetMoreAndMore")]
        static public void Get500PipesList()
        {
            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                DBDictionary nod = tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite) as DBDictionary;
                // List<PipeOld> pList = nod.GetDataFromDBdictionary<List<PipeOld>>("pipes1");
                List<PipeOld> pList = nod.GetListFromDBdictionary<PipeOld>("pipes1");

                tr.Commit();
            }
        }

        // This attribute marks this function as being command line callable 
        [CommandMethod("Write500Pipes")]
        static public void Write500Pipes()
        {
            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            _AcEd.Editor editor = _AcAp.Application.DocumentManager.MdiActiveDocument.Editor;
            _AcEd.PromptStringOptions pop = new _AcEd.PromptStringOptions("this is a test");
            _AcEd.PromptResult res = editor.GetString(pop);
            string userinput = res.StringResult;
            List<PipeOld> pList = new List<PipeOld>();
            for (int i=0;i<1000;i++) 
            {
                PipeOld p = new PipeOld();
                p.Pipeid = 1003+i;
                p.Pipename = "P003N"+ userinput + i.ToString();
                p.Poixy = new string[3] { "高", "Joanne", "Robert" };
                pList.Add(p);
            }
            using (Transaction tr = db.TransactionManager.StartTransaction()) 
            {
                DBDictionary nod = tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite) as DBDictionary;

                nod.SetDataToDBdictionary<List<PipeOld>>("pipes1",pList);

                tr.Commit();
            }
        }

        // This attribute marks this function as being command line callable 
        [CommandMethod("Get500Pipes")]
        static public void Get500Pipes()
        {
            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                DBDictionary nod = tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite) as DBDictionary;
                List<PipeOld> pList = nod.GetDataFromDBdictionary<List<PipeOld>>("pipes1");

                tr.Commit();
            }
        }

        [CommandMethod("Write500PipesOld")]
        static public void Write500PipesOld()
        {
            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            _AcEd.Editor editor = _AcAp.Application.DocumentManager.MdiActiveDocument.Editor;
            _AcEd.PromptStringOptions pop = new _AcEd.PromptStringOptions("this is a test");
            _AcEd.PromptResult res = editor.GetString(pop);
            string userinput = res.StringResult;
            List<PipeOld> pList = new List<PipeOld>();
            for (int i = 0; i < 500; i++)
            {
                PipeOld p = new PipeOld();
                p.Pipeid = 1003 + i;
                p.Pipename = "P003N" + userinput + i.ToString();
                p.Poixy = new string[3] { "高", "Joanne", "Robert" };
                pList.Add(p);
            }
            //byte[] bytes = JsonParser.Serialize(pList);
            //string strbase64 = JsonParser.ToBase64String(bytes);

            CadCommon.SetDataToDBdictionary("Testkey", pList, true);
        }

        // This attribute marks this function as being command line callable 
        [CommandMethod("Get500PipesOld")]
        static public void Get500PipesOld()
        {

            List<PipeOld> pList = (List<PipeOld>)CadCommon.GetDataFromDBdictionary("pipes1", true);
        }

        [CommandMethod("SetDataToEntity")]
        static public void SetDataToEntity()
        {

            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            _AcEd.Editor editor = _AcAp.Application.DocumentManager.MdiActiveDocument.Editor;
            PromptEntityResult per = editor.GetEntity("select a entity");
            if (per.Status == PromptStatus.OK) 
            {
                using (var tr = db.TransactionManager.StartTransaction()) 
                {
                    PipeOld p = new PipeOld();
                    p.Pipeid = 1003;
                    p.Pipename = "P003N";
                    p.Poixy = new string[3] { "高高高", "Joanne", "Robert" };
                    per.ObjectId.GetObject<DBObject>().AttachData2Entity<PipeOld>(p);

                    tr.Commit();
                }
            }
        }
        [CommandMethod("GetDataFrEntity")]
        static public void GetDataFrEntity()
        {

            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            _AcEd.Editor editor = _AcAp.Application.DocumentManager.MdiActiveDocument.Editor;
            PromptEntityResult per = editor.GetEntity("entityを選択してください。");
            if (per.Status == PromptStatus.OK)
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    PipeOld p = per.ObjectId.GetObject<DBObject>().FetchDataFrEntity<PipeOld>();

                    tr.Commit();
                }
            }
        }
        [CommandMethod("GroupObjects")]
        static public void GroupObjects()
        {

            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (var tr = db.TransactionManager.StartTransaction()) 
            {
                DBDictionary groupdic = tr.GetObject(db.GroupDictionaryId, OpenMode.ForWrite) as DBDictionary;
                Editor ed = doc.Editor;
                PromptSelectionResult psr = ed.GetSelection();
                if (psr.Status == PromptStatus.OK) 
                {
                    SelectionSet sset = psr.Value;
                    List<ObjectId> list = new List<ObjectId>();
                    foreach (ObjectId objectid in sset.GetObjectIds()) 
                    {
                        list.Add(objectid);
                    }
                    groupdic.GroupObjects(list, "GGGROUP");
                }

                tr.Commit();
            }
        }

        [CommandMethod("ConstructionSelect")]
        static public void ConstructionSelect(){

            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                DBDictionary nod = tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite) as DBDictionary;
                string constructionid = nod.GetDataFromDBdictionary<string>(Constants.CONSTRUCTION_ID);
                ed.WriteMessage(constructionid);
                tr.Commit();
            }

        }

        [CommandMethod("WriteConstruction")]
        static public void WriteConstruction()
        {

            Document doc = _AcAp.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            _AcEd.Editor editor = _AcAp.Application.DocumentManager.MdiActiveDocument.Editor;
            _AcEd.PromptStringOptions pop = new _AcEd.PromptStringOptions("工事IDを入力してください。");
            _AcEd.PromptResult res = editor.GetString(pop);
            string userinput = res.StringResult;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                DBDictionary nod = tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite) as DBDictionary;

                nod.SetDataToDBdictionary<string>(Constants.CONSTRUCTION_ID, userinput);

                tr.Commit();
            }

        }

        [CommandMethod("TestExtClass")]
        static public void TestExtClass()
        {


            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptEntityResult per = ed.GetEntity("\n選択してください： ");
            if (per.Status == PromptStatus.OK)
            {
                if (per.ObjectId.Database == db) 
                { 
                }
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    Entity ent = (Entity)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    ResultBuffer data = ent.GetExDicXrecord("TXT");
                    if (data == null)
                    {
                        ed.WriteMessage("\nentityがそんざいしない Xrecordに追加する");
                        ent.SetExdicXrecord("TXT", new TypedValue(1, "GG"), new TypedValue(70, 99));
                    }
                    else
                    {
                        foreach (TypedValue tv in data.AsArray())
                        {
                            ed.WriteMessage("\nTypeCode: {" + tv.TypeCode.ToString() + "}, Value: {" + tv.Value.ToString() + "}");
                        }
                    }
                    tr.Commit();
                }
            }
        }
    }
}
