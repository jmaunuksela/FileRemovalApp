using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security;

List<(bool delete, FileSystemInfo fsi)> removeFsiList = new List<(bool delete, FileSystemInfo fsi)>();

string[] cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToArray<string>();
for(int i = 0; i < cmdArgs.Length; i++)
{
    try
    {
        if (string.IsNullOrWhiteSpace(args[i]))
        {
            throw new ArgumentNullException();
        }
        if (File.Exists(cmdArgs[i]))
        {
            removeFsiList.Add((delete: false, fsi: new FileInfo(cmdArgs[i])));
        }
        else if (Directory.Exists(cmdArgs[i]))
        {
            removeFsiList.Add((delete: false, fsi: new DirectoryInfo(cmdArgs[i])));
        }
        else
        {
            Console.Write("IO error: The file or directory ");
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Write(cmdArgs[i]);
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(" does not ");
            Console.ResetColor();
            Console.WriteLine("exist.");
        }
    }
    catch (Exception ex) when (ex is ArgumentNullException || ex is ArgumentException || ex is NotSupportedException)
    {
        if (string.IsNullOrWhiteSpace(cmdArgs[i]))
        {
            Console.WriteLine($"Argument error: Argument #{i+1} is empty or all whitespace.");
        }
        else
        {
            Console.Write("Argument error: Erroneous file or directory name ");
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Write(cmdArgs[i]);
            Console.ResetColor();
            Console.WriteLine(".");
        }
    }
    catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException)
    {
        Console.WriteLine($"Security error: {ex.Message}");
    }
    catch (PathTooLongException)
    {
        Console.WriteLine("IO Error: Path or file name length exceeds the system set maximum limit.");
    }
}


for (int i = 0; i < removeFsiList.Count; i++)
{
    bool hasConfirmation;
    ConsoleKeyInfo userInput;
    bool removeRest = false;
    bool cancel = false;

    do
    {        
        Console.Write("Are you sure to permanently delete the ");
        if ((removeFsiList[i].fsi.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
        {
            Console.Write("directory ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(removeFsiList[i].fsi.FullName);
        }
        else
        {
            if ((((FileInfo)removeFsiList[i].fsi).Attributes & FileAttributes.System) == FileAttributes.System)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("system file ");
            }
            else
            {
                Console.Write("file ");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(removeFsiList[i].fsi.FullName);
        }

        Console.ResetColor();
        Console.WriteLine("?");
        Console.Write("[Y]es, [N]o, Yes to [A]ll, [C]ancel.");

        userInput = Console.ReadKey(true);
        Console.WriteLine();

        switch(userInput.Key)
        {
            case ConsoleKey.Y:
                removeFsiList[i] = (delete: true, fsi: removeFsiList[i].fsi);
                hasConfirmation = true;
                break;

            case ConsoleKey.N:
                removeFsiList[i] = (delete: false, fsi: removeFsiList[i].fsi);
                hasConfirmation = true;
                break;
            
            case ConsoleKey.A:
                removeRest = true;
                hasConfirmation = true;
                break;

            case ConsoleKey.C:
                cancel = true;
                hasConfirmation = true;
                break;

            default:
                Console.Beep();
                hasConfirmation = false;
                break;
        }        
    } while (!hasConfirmation);

    if (removeRest)
    {
        List<(bool delete, FileSystemInfo fsi)> tempList = removeFsiList.Skip<(bool delete, FileSystemInfo fsi)>(i).ToList<(bool delete, FileSystemInfo fsi)>();
        removeFsiList = removeFsiList.Take<(bool delete, FileSystemInfo fsi)>(i).Concat<(bool delete, FileSystemInfo fsi)>(tempList.Select<(bool delete, FileSystemInfo fsi), (bool delete, FileSystemInfo fsi)>(f => (delete: true, fsi: f.fsi))).ToList<(bool delete, FileSystemInfo fsi)>();
        break;
    }
    else if (cancel)
    {
        Console.WriteLine("Canceled, no files were deleted.");
        Environment.Exit(0);
    }
}

int fileDeleteCount = 0;
int directoryDeleteCount = 0;
foreach ((bool delete, FileSystemInfo fsi) removeFsi in removeFsiList)
{
    if (removeFsi.delete && removeFsi.fsi is DirectoryInfo)
    {
        (bool canDelete, DirectoryInfo di) di = (true, removeFsi.fsi as DirectoryInfo);
        Stack<List<(bool canDelete, DirectoryInfo di)>> recursiveRemoval = new Stack<List<(bool canDelete, DirectoryInfo di)>>();
        bool canDelete = true;

        try
        {
            foreach(FileInfo fi in di.di.GetFiles())
            {
                try
                {
                    fi.Delete();
                    fileDeleteCount++;   
                }
                catch (SystemException ex)
                {
                    Console.WriteLine($"Could not remove file {fi.FullName}: {ex.Message}");
                    canDelete = false;
                }            
            }
        }
        catch (SystemException)
        {            
            throw;
        }


        List<(bool canDelete, DirectoryInfo di)> tempList = new List<(bool canDelete, DirectoryInfo di)>();
        foreach(DirectoryInfo rdi in di.di.GetDirectories())
            tempList.Add((true, rdi));

        if (tempList.Any<(bool canDelete, DirectoryInfo di)>())
        {
            do
            {
                di = tempList[0];
                bool dirNonRemovable = false;
                bool skip = false;
                try
                {
                    FileInfo[] diFiles = di.di.GetFiles();

                    foreach(FileInfo fi in diFiles)
                    {
                        try
                        {
                            fi.Delete();
                            fileDeleteCount++;
                        }
                        catch (SystemException ex)
                        {
                            Console.WriteLine($"Could not remove file {fi.FullName}: {ex.Message}");
                            dirNonRemovable = true;
                        }
                    }
                }
                catch (SystemException ex)
                {
                    Console.WriteLine($"Could not remove directory {di.di.FullName}: {ex.Message}");                 
                    dirNonRemovable = true;
                    skip = true;
                }

                if (!skip)
                {
                    recursiveRemoval.Push(tempList);
                    tempList = new List<(bool canDelete, DirectoryInfo di)>();
                    foreach (DirectoryInfo rdi in di.di.GetDirectories())
                        tempList.Add((true, rdi));
                }
                else
                {
                    tempList.RemoveAt(0);
                }                
                
                if (dirNonRemovable)
                {                    
                    Stack<List<(bool canDelete, DirectoryInfo di)>> tempStack = new Stack<List<(bool canDelete, DirectoryInfo di)>>();
                    canDelete = false;
                    while(recursiveRemoval.Any<List<(bool canDelete, DirectoryInfo di)>>())
                    {
                        List<(bool canDelete, DirectoryInfo di)> tempRemovalList = recursiveRemoval.Pop();
                        tempRemovalList[0] = (false, tempRemovalList[0].di);
                        tempStack.Push(tempRemovalList);
                    }
                    while(tempStack.Any<List<(bool canDelete, DirectoryInfo di)>>())
                        recursiveRemoval.Push(tempStack.Pop());
                }
            
                while(!tempList.Any<(bool canDelete, DirectoryInfo di)>() && recursiveRemoval.Any<List<(bool canDelete, DirectoryInfo di)>>())
                {
                    tempList = recursiveRemoval.Pop();
                    di = tempList[0];
                    tempList.RemoveAt(0);
                    if (di.canDelete)
                    {
                        try
                        {
                            di.di.Delete();
                            directoryDeleteCount++;
                        }
                        catch (SystemException ex)
                        {                        
                            Console.WriteLine($"Could not remove directory {di.di.FullName}: {ex.Message}");
                            canDelete = false;
                            Stack<List<(bool canDelete, DirectoryInfo di)>> tempStack = new Stack<List<(bool canDelete, DirectoryInfo di)>>();
                            while(recursiveRemoval.Any<List<(bool canDelete, DirectoryInfo di)>>())
                            {
                                List<(bool canDelete, DirectoryInfo di)> tempRemovalList = recursiveRemoval.Pop();
                                tempRemovalList[0] = (false, tempRemovalList[0].di);
                                tempStack.Push(tempRemovalList);
                            }
                            while(tempStack.Any<List<(bool canDelete, DirectoryInfo di)>>())
                                recursiveRemoval.Push(tempStack.Pop());
                        }                                        
                    }
                }
            } while (recursiveRemoval.Any<List<(bool canRemove, DirectoryInfo di)>>() || tempList.Any<(bool canRemove, DirectoryInfo di)>());
        }

        if (canDelete)
        {
            try
            {
                removeFsi.fsi.Delete();
                directoryDeleteCount++;   
            }
            catch (SystemException ex)
            {
                Console.WriteLine($"Could not remove directory {removeFsi.fsi.FullName}: {ex.Message}");
            }
        }
    } 
    else if (removeFsi.delete)
    {
        try
        {
            removeFsi.fsi.Delete();
            fileDeleteCount++;
        }
        catch (SystemException ex)
        {
            Console.WriteLine($"Could not remove file {removeFsi.fsi.FullName}: {ex.Message}");
        }
    }    
}

System.Console.WriteLine("Removed " + fileDeleteCount + (fileDeleteCount == 1 ? " file" : " files") + " and " + directoryDeleteCount + (directoryDeleteCount == 1 ? " directory." : " directories."));