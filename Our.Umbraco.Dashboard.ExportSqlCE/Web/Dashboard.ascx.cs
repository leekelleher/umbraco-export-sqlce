﻿using System;
using System.Data.Common;
using System.IO;
using System.Web.UI;
using ErikEJ.SqlCeScripting;
using ICSharpCode.SharpZipLib.Zip;
using umbraco;

namespace Our.Umbraco.Dashboard.ExportSqlCE.Web
{
	public partial class Dashboard : UserControl
	{
		protected void Page_Load(object sender, EventArgs e)
		{
			if (IsPostBack)
				return;

			var builder = new DbConnectionStringBuilder() { ConnectionString = GlobalSettings.DbDSN };
			if (builder.ContainsKey("datalayer"))
			{
				var dataLayer = builder["datalayer"].ToString();

				if (string.Equals(dataLayer, "SQLCE4Umbraco.SqlCEHelper,SQLCE4Umbraco", StringComparison.OrdinalIgnoreCase))
				{
					var sqlcePath = builder.ContainsKey("datasource") ? builder["datasource"].ToString() : builder["data source"].ToString();
					this.ltrlFileName.Text = sqlcePath;
					this.phForm.Visible = true;
					return;
				}
			}

			this.phError.Visible = true;
		}

		protected void btnExport_Click(object sender, EventArgs e)
		{
			var builder = new DbConnectionStringBuilder() { ConnectionString = GlobalSettings.DbDSN };
			if (builder.ContainsKey("datalayer"))
			{
				var dataLayer = builder["datalayer"].ToString();

				if (string.Equals(dataLayer, "SQLCE4Umbraco.SqlCEHelper,SQLCE4Umbraco", StringComparison.OrdinalIgnoreCase))
				{
					builder.Remove("datalayer");

					var sqlcePath = builder.ContainsKey("datasource") ? builder["datasource"].ToString() : builder["data source"].ToString();

					// resolve the 'DataDirectory'
					if (builder.ConnectionString.Contains("|DataDirectory|"))
					{
						var dataDirectory = AppDomain.CurrentDomain.GetData("DataDirectory").ToString();

						if (!dataDirectory.EndsWith("\\"))
							dataDirectory = string.Concat(dataDirectory, "\\");

						sqlcePath = sqlcePath.Replace("|DataDirectory|", dataDirectory);
					}

					// check if file exists
					if (!File.Exists(sqlcePath))
						return; // FAIL

					// set-up some vars
					var datestamp = DateTime.UtcNow.ToString("_yyyyMMddHHmmss");
					var fileInfo = new FileInfo(sqlcePath);
					var originalFileName = fileInfo.Name.Replace(fileInfo.Extension, string.Empty);
					var exportFileName = string.Concat(originalFileName, datestamp, ".sql");
					var exportPath = Server.MapPath("~/App_Data/" + exportFileName);
					var tempPath = sqlcePath.Replace(fileInfo.Extension, string.Concat(datestamp, fileInfo.Extension));

					// make a copy of the db
					File.Copy(sqlcePath, tempPath);

					// replace the conn string
					if (builder.ContainsKey("datasource"))
					{
						builder["datasource"] = tempPath;
					}
					else
					{
						builder["data source"] = tempPath;
					}

					// export db
					using (var repository = new DB4Repository(builder.ConnectionString))
					{
						var generator = new Generator4(repository, exportPath);
						var scope = this.rblScope.SelectedValue.Equals("data") ? Scope.SchemaData : Scope.Schema;

						// save db script to file
						generator.ScriptDatabaseToFile(scope);
					}

					// delete the temp db
					File.Delete(tempPath);

					// get the bytes from the export script
					var bytes = File.ReadAllBytes(exportPath);

					// check if the export script should be zipped
					if (this.cbZip.Checked)
					{
						using (var zip = new ZipOutputStream(Response.OutputStream))
						{
							var entry = new ZipEntry(ZipEntry.CleanName(exportFileName));
							zip.PutNextEntry(entry);
							zip.Write(bytes, 0, bytes.Length);
						}

						// push the zip file to the browser
						Response.AddHeader("content-disposition", string.Concat("attachment; filename=", exportFileName, ".zip"));
						Response.ContentType = "application/zip";
					}
					else
					{
						// push the export script to the browser
						Response.AddHeader("content-disposition", string.Concat("attachment; filename=", exportFileName));
						Response.ContentType = "text/x-sql";
						Response.BinaryWrite(bytes);
					}

					Response.Flush();
					Response.End();
				}
			}
		}
	}
}