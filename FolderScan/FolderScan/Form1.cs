using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;
using System.Threading.Tasks;

namespace FolderScan
{
    public partial class Form1 : Form
    {
        int fileCount = 0;
        DateTime startTime;
        FolderTree folderTree;
        int threadCount = 4;
        Stack<FolderNode> foldersToCheck;
        CancellationTokenSource cancelToken = new CancellationTokenSource();

        public Form1()
        {
            InitializeComponent();
            folderTree = new FolderTree();
            Controls.Add(folderTree);
            folderTree.Show();
            folderTree.Dock = DockStyle.Fill;
            folderTree.BringToFront();

            foldersToCheck = new Stack<FolderNode>();
            folderTree.ContextMenu.MenuItems.Add(new MenuItem("Delete Folder", CtxtDelFolder));
            folderTree.ContextMenu.MenuItems.Add(new MenuItem("Refresh Directory", CtxtRefreshDirectory));
        }

        private void CtxtDelFolder(object sender, EventArgs e)
        {
            if (MessageBox.Show("Delete Folder '" + folderTree.Selected.Last + "'?", "Warning", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                FileSystem.DeleteDirectory(folderTree.Selected.Path, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                SubtractSizeFromParents(folderTree.Selected);
                folderTree.Selected.Parent.Nodes.Remove(folderTree.Selected);
                this.Refresh();
            }
        }

        private void CtxtRefreshDirectory(object sender, EventArgs e)
        {
            FolderNode selected = folderTree.Selected;
            SubtractSizeFromParents(selected);
            selected.Nodes.Clear();
            selected.Size = 0;
            selected.ContainedSize = 0;
            fileCount = 0;
            startTime = DateTime.Now;
            StartScan(selected);
            RefreshDisplay();
        }

        private void SubtractSizeFromParents(FolderNode n)
        {
            FolderNode current = n;
            while ((current = current.Parent) != null)
            {
                current.ContainedSize -= n.TotalSize;
                current.Text = current.Last + " [" + BytesToWhatever(current.TotalSize) + "] ";
            }
        }
        
        private void StartScan(FolderNode start)
        {
            foldersToCheck = new Stack<FolderNode>();
            foldersToCheck.Push(start);
            cancelToken = new CancellationTokenSource();

            if (ModifierKeys == Keys.Shift)
            {
                Task.Run(() => { MultiThreadScan(); });
                btnScan.Text = "Cancel";
            }
            else
            {
                Task scanTask = new Task(CheckFolders, cancelToken.Token, TaskCreationOptions.LongRunning);
                scanTask.ContinueWith((prev) => { btnScan.Text = "Scan"; });
                scanTask.Start();
                btnScan.Text = "Cancel";
            }
        }

        //WIP multithreaded version
        private void MultiThreadScan()
        {
            //initialize tasks
            Task[] tasks = new Task[4] { Task.Run(() => { }), Task.Run(() => { }), Task.Run(() => { }), Task.Run(() => { }) };
            DateTime lastUpdate = DateTime.Now;
            do
            {
                //wait for any task to be finished then if theres more work start a new task
                int i = Task.WaitAny(tasks);
                if (foldersToCheck.Count > 0)
                {
                    FolderNode n = foldersToCheck.Pop();
                    tasks[i] = Task.Run(() => { CheckFolder(n); });
                }

                if (cancelToken.IsCancellationRequested)
                {
                    Task.WaitAll(tasks);
                    break;
                }

                DateTime now = DateTime.Now;
                if ((now - lastUpdate).TotalMilliseconds > 100)
                {
                    lastUpdate = now;
                    RefreshDisplay();
                }
            }
            while ( //while work is being done or there is more work to be done
            tasks[0].Status <= TaskStatus.Running ||
            tasks[1].Status <= TaskStatus.Running ||
            tasks[2].Status <= TaskStatus.Running ||
            tasks[3].Status <= TaskStatus.Running ||
            foldersToCheck.Count > 0);

            RefreshDisplay();
            btnScan.Invoke((MethodInvoker)(() =>
            {
                btnScan.Text = "Scan";
            }));
        }

        private void CheckFolders()
        {
            DateTime lastUpdate = DateTime.Now;
            while (true)
            {
                if (foldersToCheck.Count > 0)
                {
                    CheckFolder(foldersToCheck.Pop());
                    //only refresh every 100 milliseconds
                    DateTime now = DateTime.Now;
                    if ((now - lastUpdate).TotalMilliseconds > 100)
                    {
                        lastUpdate = now;
                        RefreshDisplay();
                    }
                }
                else
                    break;
                if (cancelToken.IsCancellationRequested)
                    break;
            }
            //refresh and reset button at the end
            RefreshDisplay();
            btnScan.Invoke((MethodInvoker)(() =>
            {
                btnScan.Text = "Scan";
            }));
        }

        //updates the foldernode with its files/folder info and adds folders to stack to be checked
        private void CheckFolder(FolderNode nodeToCheck)
        {
            try
            {
                long folderSize = 0;
                int folderFileCount = 0;
                //count files and add up sizes;
                foreach (string file in Directory.GetFiles(nodeToCheck.Path))
                {
                    folderSize += new FileInfo(file).Length;
                    folderFileCount++;
                }
                if (folderSize > 0)
                {
                    folderTree.Invoke((MethodInvoker)(() =>
                    {
                        //update current node with its filesize then sort its place in its parent
                        nodeToCheck.Size = folderSize;
                        nodeToCheck.AddNode(new FolderNode(folderFileCount + " Files: " + BytesToWhatever(folderSize)) { Size = long.MaxValue });
                        nodeToCheck.Text = nodeToCheck.Last + " [" + BytesToWhatever(nodeToCheck.TotalSize) + "]";
                        SortNode(nodeToCheck);

                        //update all parents with current nodes size and sort them
                        FolderNode temp = nodeToCheck.Parent;
                        while (temp != null)
                        {
                            temp.ContainedSize += folderSize;
                            temp.Text = temp.Last + " [" + BytesToWhatever(temp.TotalSize) + "]";
                            SortNode(temp);
                            temp = temp.Parent;
                        }
                    }));
                    fileCount += folderFileCount;
                }
                //distribute current nodes folders evenly among threads
                foreach (string folder in Directory.GetDirectories(nodeToCheck.Path))
                {
                    string folderName = (folder.Split('\\')).Last();
                    FolderNode temp = new FolderNode(folder);
                    folderTree.Invoke((MethodInvoker)(() =>
                    {
                        nodeToCheck.AddNode(temp);
                    }));
                    foldersToCheck.Push(temp);
                }
            }
            catch (Exception e) //should only happen if checking folder without permission
            {

            }
        }

        private void RefreshDisplay()
        {
            this.Invoke((MethodInvoker)(() =>
            {
                this.Text = "Elapsed Time: " + (DateTime.Now - startTime).Hours + ":" + (DateTime.Now - startTime).Minutes + ":" + (DateTime.Now - startTime).Seconds + "." + (DateTime.Now - startTime).Milliseconds + " Files: " + fileCount;
            }));
            folderTree.Invoke((MethodInvoker)(() =>
            {
                folderTree.Refresh();
            }));
        }

        //sorts node within its parents list of nodes
        private static void SortNode(FolderNode node)
        {
            FolderNode parent = node.Parent;
            if (parent != null)
            {
                int index = parent.Nodes.IndexOf(node);
                for (int i = 0; i < parent.Nodes.Count; i++)
                {
                    if (parent.Nodes[i].TotalSize < node.TotalSize)
                    {
                        if (i != index+1)
                        {
                            parent.Nodes.Remove(node);
                            parent.Nodes.Insert(i, node);
                        }
                        break;
                    }
                }
            }
        }

        private static string BytesToWhatever(long bytes)
        {
            double temp = bytes;
            string unit = " B";
            if (bytes > 1073741824) { temp /= 1073741824; unit = " GB"; }
            else if (bytes > 1048576) { temp /= 1048576; unit = " MB"; }
            else if (bytes > 1024) { temp /= 1024; unit = " KB"; }

            return DecIfLess10(temp) + unit;
        }

        private static double DecIfLess10(double num)
        {
            if (num > 10)
                return (int)num;
            return Math.Round(num, 2);
        }

        private void btnScan_Click(object sender, EventArgs e)
        {
            if (btnScan.Text == "Scan")
            {
                //add root node, initialize stuff, start the scan
                folderTree.Nodes.Clear();
                fileCount = 0;
                startTime = DateTime.Now;
                string loc = txtStart.Text.Replace('/', '\\');
                folderTree.AddNode(new FolderNode(loc + @"\", loc));
                folderTree.Nodes[0].Open = true;
                folderTree.Nodes[0].Text = folderTree.Nodes[0].Last;
                StartScan(folderTree.Nodes[0]);
            }
            else
            {
                cancelToken.Cancel();
                btnScan.Text = "Scan";
            }
        }
    }
}
