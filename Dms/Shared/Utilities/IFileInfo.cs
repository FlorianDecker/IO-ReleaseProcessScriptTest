// This file is part of re-vision (www.re-motion.org)
// Copyright (C) 2005-2009 rubicon informationstechnologie gmbh, www.rubicon.eu
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License version 3.0 
// as published by the Free Software Foundation.
// 
// This program is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program; if not, see http://www.gnu.org/licenses.
// 
// Additional permissions are listed in the file re-motion_exceptions.txt.
// 
using System;
using System.IO;
using System.Security.AccessControl;

namespace Remotion.Dms.Shared.Utilities
{
  public interface IFileInfo
  {
    string FullName { get; }
    string Extension { get; }
    string Name { get; }
    long Length { get; }
    string DirectoryName { get; }
    DirectoryInfo Directory { get; }
    bool IsReadOnly { get; }
    bool Exists { get; }
    DateTime CreationTime { get; }
    DateTime LastAccessTime { get; }
    DateTime LastWriteTime { get; }
    Stream Open (FileMode mode, FileAccess access, FileShare share);
    
  }
}