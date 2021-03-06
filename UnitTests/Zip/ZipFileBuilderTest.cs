// This file is part of the re-motion Framework (www.re-motion.org)
// Copyright (c) rubicon IT GmbH, www.rubicon.eu
// 
// re-motion is free software; you can redistribute it and/or modify it under 
// the terms of the GNU Lesser General Public License as published by the 
// Free Software Foundation; either version 2.1 of the License, 
// or (at your option) any later version.
// 
// re-motion is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with re-motion; if not, see http://www.gnu.org/licenses.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using JetBrains.Annotations;
using NUnit.Framework;
using Remotion.Development.UnitTesting.IO;
using Remotion.IO.Zip;
using Remotion.Utilities;
using Rhino.Mocks;

namespace Remotion.IO.UnitTests.Zip
{
  [TestFixture]
  public class ZipFileBuilderTest
  {
    private class NotClosableMemeoryStream : MemoryStream
    {
      public NotClosableMemeoryStream ([NotNull] byte[] buffer)
        : base (buffer)
      {
      }

      protected override void Dispose (bool disposing)
      {
      }

      public override void Close ()
      {
      }
    }

    private TempFile _file1;
    private TempFile _file2;
    private string _folder;
    private string _path;
    private string _destinationPath;

    [SetUp]
    public void SetUp ()
    {
      _file1 = new TempFile();
      _file2 = new TempFile();
      var bytes = new byte[8191];
      for (int i = 0; i < 8191; i++)
        bytes[i] = (byte) i;

      _file1.WriteAllBytes (bytes);
      _file2.WriteAllBytes (bytes);

      _folder = Path.GetRandomFileName();
      _path = Path.Combine (Path.GetTempPath(), _folder);
      var directory = Directory.CreateDirectory (_path);

      File.Copy (_file1.FileName, Path.Combine (directory.FullName, Path.GetFileName (_file1.FileName)), true);
      File.Copy (_file2.FileName, Path.Combine (directory.FullName, Path.GetFileName (_file2.FileName)), true);

      ZipConstants.DefaultCodePage = Encoding.ASCII.CodePage;
    }

    [TearDown]
    public void TearDown ()
    {
      ZipConstants.DefaultCodePage = 0;

      _file1.Dispose();
      _file2.Dispose();
      Directory.Delete (_path, true);
      if (Directory.Exists (_destinationPath))
        Directory.Delete (_destinationPath, true);
    }

    [Test]
    public void BuildReturnsZipFileWithFiles ()
    {
      var zipBuilder = new ZipFileBuilder();
      zipBuilder.Progress += ((sender, e) => { });
      zipBuilder.AddFile (new FileInfoWrapper (new FileInfo (_file1.FileName)));
      zipBuilder.AddFile (new FileInfoWrapper (new FileInfo (_file2.FileName)));

      var zipFileName = Path.GetTempFileName();

      using (zipBuilder.Build (zipFileName))
      {
      }

      var expectedFiles = new List<string> { Path.GetFileName (_file1.FileName), Path.GetFileName (_file2.FileName) };
      CheckUnzippedFiles (zipFileName, expectedFiles);
    }

    [Test]
    public void BuildReturnsZipFileWithEmptyFile_WithDiskFile ()
    {
      using (var fileEmpty = new TempFile ())
      {
        var zipBuilder = new ZipFileBuilder();
        zipBuilder.Progress += ((sender, e) => { });
        zipBuilder.AddFile (new FileInfoWrapper (new FileInfo (fileEmpty.FileName)));

        var zipFileName = Path.GetTempFileName();

        using (zipBuilder.Build (zipFileName))
        {
        }

        var expectedFiles = new[] { Path.GetFileName (fileEmpty.FileName) };
        CheckUnzippedFiles (zipFileName, expectedFiles);
      }
    }

    [Test]
    public void BuildReturnsZipFileWithEmptyFile_FromInMemoryFile ()
    {
      var zipBuilder = new ZipFileBuilder();
      zipBuilder.Progress += ((sender, e) => { });
      zipBuilder.AddFile (new InMemoryFileInfo ("TheFile", new MemoryStream (new byte[0]), null, DateTime.Today, DateTime.Today, DateTime.Today));

      var zipFileName = Path.GetTempFileName();

      using (zipBuilder.Build (zipFileName))
      {
      }

      var expectedFiles = new[] { "TheFile" };
      CheckUnzippedFiles (zipFileName, expectedFiles);
    }

    [Test]
    public void BuildReturnsZipFileWithFileWithUmlaut ()
    {
      string fileWithUmlautInName = Path.Combine (Path.GetTempPath(), "NameWith�.txt");
      File.WriteAllText (fileWithUmlautInName, "Hello World!");

      try
      {
        var zipBuilder = new ZipFileBuilder();
        zipBuilder.Progress += ((sender, e) => { });
        zipBuilder.AddFile (new FileInfoWrapper (new FileInfo (fileWithUmlautInName)));

        var zipFileName = Path.GetTempFileName();

        using (zipBuilder.Build (zipFileName))
        {
        }

        var expectedFiles = new List<string> { Path.GetFileName (fileWithUmlautInName) };
        CheckUnzippedFiles (zipFileName, expectedFiles);
      }
      finally
      {
        FileUtility.DeleteAndWaitForCompletion (fileWithUmlautInName);
      }
    }

    [Test]
    [ExpectedException (typeof (IOException))]
    public void NoHandlerForArchiveError_ThrowsException ()
    {
      var zipBuilder = new ZipFileBuilder();
      zipBuilder.Progress += ((sender, e) => { });

      var fileInfoMock = MockRepository.GenerateMock<IFileInfo>();
      fileInfoMock.Expect (mock => mock.FullName).Return (@"C:\fileName");
      fileInfoMock.Expect (mock => mock.Open (FileMode.Open, FileAccess.Read, FileShare.Read)).Throw (new IOException ("ioexception"));
      fileInfoMock.Stub (mock => mock.Directory).Return (new DirectoryInfoWrapper (new DirectoryInfo (@"C:\")));

      zipBuilder.AddFile (fileInfoMock);
      var zipFileName = Path.GetTempFileName();
      try
      {
        using (zipBuilder.Build (zipFileName))
        {
        }
      }
      finally
      {
        FileUtility.DeleteAndWaitForCompletion (zipFileName);
      }
    }


    [Test]
    [ExpectedException (typeof (AbortException))]
    public void SetFileProcessingRecoveryAction_Abort ()
    {
      var zipBuilder = new ZipFileBuilder();
      zipBuilder.Progress += ((sender, e) => { });

      var fileInfoMock = MockRepository.GenerateMock<IFileInfo>();
      fileInfoMock.Expect (mock => mock.FullName).Return (@"C:\fileName");
      fileInfoMock.Expect (mock => mock.Open (FileMode.Open, FileAccess.Read, FileShare.Read)).Throw (new IOException());
      fileInfoMock.Stub (mock => mock.Directory).Return (new DirectoryInfoWrapper (new DirectoryInfo (@"C:\")));

      zipBuilder.AddFile (fileInfoMock);

      zipBuilder.Error += ((sender, e) => zipBuilder.FileProcessingRecoveryAction = FileProcessingRecoveryAction.Abort);

      var zipFileName = Path.GetTempFileName();
      try
      {
        using (zipBuilder.Build (zipFileName))
        {
        }
      }
      finally
      {
        FileUtility.DeleteAndWaitForCompletion (zipFileName);
      }
    }

    [Test]
    public void SetFileProcessingAction_Ignore ()
    {
      var zipBuilder = new ZipFileBuilder();
      zipBuilder.Progress += ((sender, e) => { });

      var fileInfoMock = MockRepository.GenerateMock<IFileInfo>();

      fileInfoMock.Expect (mock => mock.FullName).Return (@"C:\fileName");
      fileInfoMock.Expect (mock => mock.Open (FileMode.Open, FileAccess.Read, FileShare.Read)).Throw (new IOException());
      fileInfoMock.Stub (mock => mock.Directory).Return (new DirectoryInfoWrapper (new DirectoryInfo (@"C:\")));

      zipBuilder.AddFile (new FileInfoWrapper (new FileInfo (_file1.FileName)));
      zipBuilder.AddFile (fileInfoMock);

      zipBuilder.Error += ((sender, e) => zipBuilder.FileProcessingRecoveryAction = FileProcessingRecoveryAction.Ignore);
      var zipFileName = Path.GetTempFileName();
      using (zipBuilder.Build (zipFileName))
      {
      }

      var expectedFiles = new List<string> { Path.GetFileName (_file1.FileName) };
      CheckUnzippedFiles (zipFileName, expectedFiles);
    }

    [Test]
    public void SetFileProcessingAction_Retry ()
    {
      var zipBuilder = new ZipFileBuilder();
      zipBuilder.Progress += ((sender, e) => { });
      zipBuilder.Error += ((sender, e) => zipBuilder.FileProcessingRecoveryAction = FileProcessingRecoveryAction.Retry);

      var fileInfoStub = MockRepository.GenerateStub<IFileInfo>();

      zipBuilder.AddFile (new FileInfoWrapper (new FileInfo (_file1.FileName)));
      fileInfoStub.Stub (stub => stub.FullName).Return (_file2.FileName);
      fileInfoStub.Stub (stub => stub.Name).Return (Path.GetFileName (_file2.FileName));
      fileInfoStub.Stub (stub => stub.Length).Return (_file2.Length);
      zipBuilder.AddFile (fileInfoStub);

      fileInfoStub.Expect (mock => mock.Open (FileMode.Open, FileAccess.Read, FileShare.Read)).Throw (new IOException()).Repeat.Once();

      var fileInfo = new FileInfoWrapper (new FileInfo (_file2.FileName));
      var stream = fileInfo.Open (FileMode.Open, FileAccess.Read, FileShare.Read);
      fileInfoStub.Expect (mock => mock.Open (FileMode.Open, FileAccess.Read, FileShare.Read)).Return (stream);

      fileInfoStub.Stub (mock => mock.Directory).Return (new DirectoryInfoWrapper (new DirectoryInfo (Path.GetDirectoryName (_file2.FileName))));

      var zipFileName = Path.GetTempFileName();
      using (zipBuilder.Build (zipFileName))
      {
      }
      var expectedFiles = new List<string> { Path.GetFileName (_file1.FileName), Path.GetFileName (_file2.FileName) };
      CheckUnzippedFiles (zipFileName, expectedFiles);
    }

    [Test]
    public void BuildReturnsZipFileWithFolder ()
    {
      var zipBuilder = new ZipFileBuilder();
      zipBuilder.Progress += ((sender, e) => { });
      zipBuilder.AddDirectory (new DirectoryInfoWrapper (new DirectoryInfo (_path)));

      var zipFileName = Path.GetTempFileName();
      using (zipBuilder.Build (zipFileName))
      {
      }

      var expectedFiles = new List<string> { Path.GetFileName (_file1.FileName), Path.GetFileName (_file2.FileName) };
      CheckUnzippedFiles (zipFileName, expectedFiles);
    }

    [Test]
    public void BuildReturnsZipFilesWithFoldersAndFiles ()
    {
      //complex
      //-file1
      //-Directory1
      //--file2
      //--file3
      //-Directory2
      //--Directory3 �
      //---file4
      //---file5
      //--file6

      var file1 = new TempFile();
      var file2 = new TempFile();
      var file3 = new TempFile();
      var file4 = new TempFile();
      var file5 = new TempFile();
      var file6 = new TempFile();

      var bytes = new byte[8191];
      for (int i = 0; i < 8191; i++)
        bytes[i] = (byte) i;

      file1.WriteAllBytes (bytes);
      file2.WriteAllBytes (bytes);
      file3.WriteAllBytes (bytes);
      file4.WriteAllBytes (bytes);
      file5.WriteAllBytes (bytes);
      file6.WriteAllBytes (bytes);

      var rootPath = Path.Combine (Path.GetTempPath(), Guid.NewGuid().ToString());

      var directory1 = Directory.CreateDirectory (Path.Combine (rootPath, "Directory1"));
      var directory2 = Directory.CreateDirectory (Path.Combine (rootPath, "Directory2"));
      var directory3 = Directory.CreateDirectory (Path.Combine (directory2.FullName, "Directory3 �"));

      File.Move (file1.FileName, Path.Combine (rootPath, Path.GetFileName (file1.FileName)));
      File.Move (file2.FileName, Path.Combine (directory1.FullName, Path.GetFileName (file2.FileName)));
      File.Move (file3.FileName, Path.Combine (directory1.FullName, Path.GetFileName (file3.FileName)));
      File.Move (file4.FileName, Path.Combine (directory3.FullName, Path.GetFileName (file4.FileName)));
      File.Move (file5.FileName, Path.Combine (directory3.FullName, Path.GetFileName (file5.FileName)));
      File.Move (file6.FileName, Path.Combine (directory2.FullName, Path.GetFileName (file6.FileName)));

      var zipFileName = Path.GetTempFileName();

      var zipBuilder = new ZipFileBuilder();
      zipBuilder.Progress += ((sender, e) => { });
      zipBuilder.AddDirectory (new DirectoryInfoWrapper (new DirectoryInfo (rootPath)));

      using (zipBuilder.Build (zipFileName))
      {
      }
      var expectedFiles = new List<string>
                          {
                              Path.GetFileName (file1.FileName),
                              Path.GetFileName (file2.FileName),
                              Path.GetFileName (file3.FileName),
                              Path.GetFileName (file4.FileName),
                              Path.GetFileName (file5.FileName),
                              Path.GetFileName (file6.FileName)
                          };

      try
      {
        CheckUnzippedFiles (zipFileName, expectedFiles);
      }
      finally
      {
        Directory.Delete (rootPath, true);
      }
    }

    [Test]
    [ExpectedException (typeof (AbortException))]
    public void BuildThrowsAbortExceptionUponCancel ()
    {
      var zipBuilder = new ZipFileBuilder ();
      zipBuilder.Progress += ((sender, e) => { e.Cancel = e.CurrentFileValue > 1000; });
      zipBuilder.AddFile (new FileInfoWrapper (new FileInfo (_file1.FileName)));

      var zipFileName = Path.GetTempFileName ();

      using (zipBuilder.Build (zipFileName))
      {
      }
    }

    [Test]
    public void BuildReportsProperly ()
    {
      var root =
          CreateDirectory ("root",
              CreateFile ("file1", 10),
              CreateFile ("file2", 20),
              CreateDirectory ("dir1"),
              CreateDirectory ("dir2",
                  CreateFile ("file1", 30),
                  CreateFile ("file2", 40),
                  CreateDirectory ("dir2",
                      CreateFile ("file1", 50),
                      CreateFile ("file2", 60),
                      CreateDirectory ("dir2",
                          CreateFile ("file1", 70),
                          CreateFile ("file2", 80)))),
              CreateDirectory ("dir3",
                  CreateFile ("file1", 90))) (null);

      var zipBuilder = new ZipFileBuilder();

      root.Files.ForEach (zipBuilder.AddFile);
      root.Directories.ForEach (zipBuilder.AddDirectory);

      var progressArgs = new List<ArchiveBuilderProgressEventArgs>();

      zipBuilder.Progress += (sender, e) => progressArgs.Add (e);

      var zipFileName = Path.GetTempFileName();
      try
      {
        using (zipBuilder.Build (zipFileName))
        {
        }
      }
      finally
      {
        File.Delete (zipFileName);
      }

      Assert.That (progressArgs.Count, Is.EqualTo (18));

      AssertBuildProgress (progressArgs[0], 10, 10, 0, @"root\file1");
      AssertBuildProgress (progressArgs[1], 10, 10, 0, @"root\file1");
      AssertBuildProgress (progressArgs[2], 30, 20, 1, @"root\file2");
      AssertBuildProgress (progressArgs[3], 30, 20, 1, @"root\file2");
      AssertBuildProgress (progressArgs[4], 60, 30, 2, @"root\dir2\file1");
      AssertBuildProgress (progressArgs[5], 60, 30, 2, @"root\dir2\file1");
      AssertBuildProgress (progressArgs[6], 100, 40, 3, @"root\dir2\file2");
      AssertBuildProgress (progressArgs[7], 100, 40, 3, @"root\dir2\file2");
      AssertBuildProgress (progressArgs[8], 150, 50, 4, @"root\dir2\dir2\file1");
      AssertBuildProgress (progressArgs[9], 150, 50, 4, @"root\dir2\dir2\file1");
      AssertBuildProgress (progressArgs[10], 210, 60, 5, @"root\dir2\dir2\file2");
      AssertBuildProgress (progressArgs[11], 210, 60, 5, @"root\dir2\dir2\file2");
      AssertBuildProgress (progressArgs[12], 280, 70, 6, @"root\dir2\dir2\dir2\file1");
      AssertBuildProgress (progressArgs[13], 280, 70, 6, @"root\dir2\dir2\dir2\file1");
      AssertBuildProgress (progressArgs[14], 360, 80, 7, @"root\dir2\dir2\dir2\file2");
      AssertBuildProgress (progressArgs[15], 360, 80, 7, @"root\dir2\dir2\dir2\file2");
      AssertBuildProgress (progressArgs[16], 450, 90, 8, @"root\dir3\file1");
      AssertBuildProgress (progressArgs[17], 450, 90, 8, @"root\dir3\file1");
    }

    [Test]
    public void Build_FileSizeIsGreaterThanBufferSize_ReportsProperly ()
    {
      var root =
          CreateDirectory ("root",
              CreateFile ("file1", StreamCopier.DefaultCopyBufferSize + 1000),
              CreateFile ("file2", StreamCopier.DefaultCopyBufferSize + 2000)) (null);

      var zipBuilder = new ZipFileBuilder();

      root.Files.ForEach (zipBuilder.AddFile);
      var progressArgs = new List<ArchiveBuilderProgressEventArgs>();

      zipBuilder.Progress += (sender, e) => progressArgs.Add (e);

      var zipFileName = Path.GetTempFileName();
      try
      {
        using (zipBuilder.Build (zipFileName))
        {
        }
      }
      finally
      {
        File.Delete (zipFileName);
      }

      Assert.That (progressArgs.Count, Is.EqualTo (6));

      AssertBuildProgress (progressArgs[0], StreamCopier.DefaultCopyBufferSize, StreamCopier.DefaultCopyBufferSize, 0, @"root\file1");
      AssertBuildProgress (progressArgs[1], StreamCopier.DefaultCopyBufferSize + 1000, StreamCopier.DefaultCopyBufferSize + 1000, 0, @"root\file1");
      AssertBuildProgress (progressArgs[2], StreamCopier.DefaultCopyBufferSize + 1000, StreamCopier.DefaultCopyBufferSize + 1000, 0, @"root\file1");
      AssertBuildProgress (progressArgs[3], 2 * StreamCopier.DefaultCopyBufferSize + 1000, StreamCopier.DefaultCopyBufferSize, 1, @"root\file2");
      AssertBuildProgress (progressArgs[4], 2 * StreamCopier.DefaultCopyBufferSize + 1000 + 2000, StreamCopier.DefaultCopyBufferSize + 2000, 1, @"root\file2");
      AssertBuildProgress (progressArgs[5], 2 * StreamCopier.DefaultCopyBufferSize + 1000 + 2000, StreamCopier.DefaultCopyBufferSize + 2000, 1, @"root\file2");
    }

    private void AssertBuildProgress (ArchiveBuilderProgressEventArgs args, long expectedTotalValue, long expectedCurrentFileValue, int expectedFileIndex, string expectedFileFullName)
    {
      Assert.That (args.CurrentTotalValue, Is.EqualTo (expectedTotalValue));
      Assert.That (args.CurrentFileValue, Is.EqualTo (expectedCurrentFileValue));
      Assert.That (args.CurrentFileIndex, Is.EqualTo (expectedFileIndex));
      Assert.That (args.CurrentFileFullName, Is.EqualTo (expectedFileFullName));
    }

    private Func<IDirectoryInfo, IFileInfo> CreateFile (string name, int size)
    {
      return parent => new InMemoryFileInfo (
          Path.Combine (parent.FullName, name),
          new NotClosableMemeoryStream (new byte[size]),
          parent,
          DateTime.Now,
          DateTime.Now,
          DateTime.Now);
    }

    private Func<IDirectoryInfo, InMemoryDirectoryInfo> CreateDirectory (
        string name,
        params Func<IDirectoryInfo, IFileSystemEntry>[] subEntries)
    {
      return parent =>
      {
        var directory = new InMemoryDirectoryInfo (
            parent != null ? Path.Combine (parent.FullName, name) : name,
            parent,
            DateTime.Now,
            DateTime.Now,
            DateTime.Now);

        foreach (var subEntry in subEntries)
        {
          var value = subEntry (directory);

          var dir = value as IDirectoryInfo;
          if (dir != null)
          {
            directory.Directories.Add (dir);
          }
          else
          {
            var file = value as IFileInfo;
            if (file != null)
              directory.Files.Add (file);
          }
        }

        return directory;
      };
    }

    private void CheckUnzippedFiles (string zipFileName, IList<string> expectedFiles)
    {
      var files = UnZipFile (zipFileName);
      try
      {
        Assert.That (files.Values.Count, Is.EqualTo (expectedFiles.Count));

        for (int i = 0; i < files.Values.Count; i++)
          Assert.That (files.Values.Contains (expectedFiles[i]));
      }
      finally
      {
        if (files != null)
          CleanupTempFiles (files.Keys.ToList(), zipFileName);
      }
    }

    private void CleanupTempFiles (IEnumerable<string> files, string zipFileName)
    {
      foreach (var file in files)
        FileUtility.DeleteAndWaitForCompletion (file);
      FileUtility.DeleteAndWaitForCompletion (zipFileName);
    }

    private Dictionary<string, string> UnZipFile (string zipFile)
    {
      FastZip fastZip = new FastZip();
      _destinationPath = Path.Combine (Path.GetTempPath(), Guid.NewGuid().ToString());
      fastZip.ExtractZip (zipFile, _destinationPath, FastZip.Overwrite.Always, null, null, null, false);
      List<string> files = new List<string>();
      files.AddRange (Directory.GetFiles (_destinationPath, "*", SearchOption.AllDirectories));
      var reducedFile = new Dictionary<string, string>();
      foreach (var file in files)
        reducedFile.Add (file, Path.GetFileName (file));
      return reducedFile;
    }
  }
}