using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;

namespace WixCreator
{
	class DirectoryEntry
	{
		public DirectoryEntry(string root, string path)
		{
			fullPath = path;
			folderName = Path.GetFileName(path);
			id = Program.ShortenUniqueComponentId(Path.GetRelativePath(root, path));
			children = new List<DirectoryEntry>();
		}
		public string fullPath;
		public string folderName;
		public string id;
		public List<DirectoryEntry> children;
	}
	class Program
	{
		static HashSet<string> componentIds = new HashSet<string>();
		public static string ShortenUniqueComponentId(string componentId)
		{
			string[] parts = SanitizeId(componentId).Split(new char[] { '.', '_' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = parts.Length - 1; i >= 0; i--)
			{
				StringBuilder shortId = new StringBuilder();
				for(int j=0;j<=i;j++)
				{
					shortId.Append(parts[j][0]);
					shortId.Append('.');
				}
				for(int j=i+1;j<parts.Length;j++)
				{
					shortId.Append(parts[j]);
					shortId.Append('.');
				}
				string candidate = shortId.ToString();
				if(!componentIds.Contains(candidate))
				{
					componentIds.Add(candidate);
					return candidate;
				}
			}
			if(componentId.Contains(componentId))
			{
				throw new Exception("Duplicate id");
			}
			componentIds.Add(componentId);
			return componentId;
		}

		public static string SanitizeId(string file)
		{
			return "X" + file.ToLower().Replace('\\', '.').Replace('/', '.').Replace(':', '.').Replace('-', '.').Replace(' ', '.').Replace('(', '.').Replace(')', '.');
		}

		static DirectoryEntry BuildDirectoryTree(string root, string path)
		{
			DirectoryEntry entry = new DirectoryEntry(root, path);
			foreach (string directory in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
			{
				if (!Path.GetFullPath(path).Equals(Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase))
				{
					entry.children.Add(BuildDirectoryTree(root, directory));
				}
			}
			return entry;
		}
		static void WriteIndent(StringBuilder builder, int indent)
		{
			for(int i=0;i<indent;i++)
			{
				builder.Append('\t');
			}
		}

		static void WriteFolderTree(DirectoryEntry entry, StringBuilder builder, int indent)
		{
			WriteIndent(builder, indent);
			builder.Append("<Directory Id=\"");
			builder.Append(entry.id);
			builder.Append("\" Name=\"");
			builder.Append(entry.folderName);
			builder.Append("\"");
			if (entry.children.Count > 0)
			{
				builder.AppendLine(">");
				foreach (DirectoryEntry child in entry.children)
				{
					WriteFolderTree(child, builder, indent + 1);
				}
				WriteIndent(builder, indent);
				builder.AppendLine("</Directory>");
			}
			else
			{
				builder.AppendLine("/>");
			}
		}

		static void WriteComponent(string root, DirectoryEntry entry, StringBuilder builder, int indent)
		{
			WriteIndent(builder, indent);
			builder.Append("<ComponentGroup Id=\"");
			builder.Append("dc");
			builder.Append(entry.id);
			builder.Append("\" Directory=\"");
			builder.Append(entry.id);
			builder.AppendLine("\">");

			foreach (string file in Directory.EnumerateFiles(entry.fullPath))
			{
				string fileId = ShortenUniqueComponentId(Path.GetRelativePath(root, file));
				WriteIndent(builder, indent + 1);
				builder.Append("<Component Id=\"");
				builder.Append("fc");
				builder.Append(fileId);
				builder.Append("\" Guid=\"");
				builder.Append(Guid.NewGuid());
				builder.AppendLine("\" >");

				WriteIndent(builder, indent + 3);
				builder.Append("<File Id=\"");
				builder.Append(fileId);
				builder.Append("\" Source=\"");
				builder.Append(file);
				builder.AppendLine("\" KeyPath=\"yes\" Checksum=\"yes\" />");
				
				WriteIndent(builder, indent + 1);
				builder.AppendLine("</Component>");
			}

			WriteIndent(builder, indent);
			builder.AppendLine("</ComponentGroup>");
		}

		static void WriteComponentTree(string root, DirectoryEntry entry, StringBuilder builder, int indent)
		{
			WriteComponent(root, entry, builder, indent);
			foreach(DirectoryEntry child in entry.children)
			{
				WriteComponentTree(root, child, builder, indent);
			}
		}

		static void WriteComponentRefTree(string root, DirectoryEntry entry, StringBuilder builder, int indent)
		{
			WriteIndent(builder, indent);
			builder.Append("<ComponentGroupRef Id=\"");
			builder.Append("dc");
			builder.Append(entry.id);
			builder.AppendLine("\" />");
			foreach (DirectoryEntry child in entry.children)
			{
				WriteComponentRefTree(root, child, builder, indent);
			}
		}

		static void Main(string[] args)
		{
			string parentPath = "C:/Users/alan_/Downloads/google-cloud-sdk";
			string rootPath = "C:/Users/alan_/Downloads/google-cloud-sdk/google-cloud-sdk";
			DirectoryEntry root = BuildDirectoryTree(parentPath, rootPath);

			StringBuilder builder = new StringBuilder();
			builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
			builder.AppendLine("<Wix xmlns=\"http://schemas.microsoft.com/wix/2006/wi\">");
			builder.AppendLine("\t<Product Id=\"*\" Name=\"SetupProject1\" Language=\"1033\" Version=\"1.0.0.0\" Manufacturer=\"Google\" UpgradeCode=\"4fbd35d3-d445-4896-81d7-a90ed8889174\">");
			builder.AppendLine("\t\t<Package InstallerVersion=\"200\" Compressed=\"yes\" InstallScope=\"perMachine\" />");

			builder.AppendLine("\t\t<MajorUpgrade DowngradeErrorMessage=\"A newer version of [ProductName] is already installed.\" />");
			builder.AppendLine("\t\t<MediaTemplate />");

			builder.AppendLine("\t\t<Feature Id=\"ProductFeature\" Title=\"SetupProject1\" Level=\"1\">");
			WriteComponentRefTree(parentPath, root, builder, 3);
			builder.AppendLine("\t\t</Feature>");
			builder.AppendLine("\t</Product>");

			builder.AppendLine("\t<Fragment>");
			builder.AppendLine("\t\t<Directory Id=\"TARGETDIR\" Name=\"SourceDir\">");
			builder.AppendLine("\t\t\t<Directory Id=\"ProgramFilesFolder\">");

			WriteFolderTree(root, builder, 3);

			builder.AppendLine("\t\t\t</Directory>");
			builder.AppendLine("\t\t</Directory>");
			builder.AppendLine("\t</Fragment>");

			builder.AppendLine("\t<Fragment>");

			WriteComponentTree(parentPath, root, builder, 2);
			builder.AppendLine("\t</Fragment>");
			builder.AppendLine("</Wix>");
			File.WriteAllText("C:/Users/alan_/source/repos/SetupProject1/SetupProject1/Product.wxs", builder.ToString());
		}
	}
}
