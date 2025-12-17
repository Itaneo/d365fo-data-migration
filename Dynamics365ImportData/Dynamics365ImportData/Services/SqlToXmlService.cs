namespace Dynamics365ImportData.Services;

using Dynamics365ImportData.DependencySorting;
using Dynamics365ImportData.XmlOutput;

using Microsoft.Extensions.Logging;

using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading;

internal class SqlToXmlService
{
    private readonly ILogger<SqlToXmlService> _logger;

    public SqlToXmlService(ILogger<SqlToXmlService> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<IXmlOutputPart>> ExportToOutput(SourceQueryItem source,
                                                          IXmlOutputFactory outputFactory,
                                                          CancellationToken cancellationToken)
    {
        List<IXmlOutputPart> partNames = new();
        Stopwatch watch = new();
        watch.Start();
        try
        {
            using SqlConnection con = new(source.SourceConnectionString);
            await con.OpenAsync(cancellationToken);
            string sqlStatement = await File.ReadAllTextAsync(source.QueryFileName, cancellationToken);
            int skip = 0;
            int take = source.RecordsPerFile;
            int part = 0;
            using SqlCommand cmd = con.CreateCommand();
            cmd.CommandTimeout = 3600;
            cmd.CommandText = sqlStatement;
            using SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
            IXmlOutputPart? output = await StartDocumentAsync(outputFactory, source, part++, cancellationToken);
            partNames.Add(output);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (skip > 0 && source.RecordsPerFile > 0 && skip % source.RecordsPerFile == 0)
                {
                    if (output != null)
                    {
                        await CloseDocumentWriterAsync(output, cancellationToken);
                        output = null;
                    }
                    output = await StartDocumentAsync(outputFactory, source, part++, cancellationToken);
                    partNames.Add(output);
                }

                await output.Writer.WriteStartElementAsync(string.Empty, source.EntityName, string.Empty);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    output.Writer.WriteStartAttribute(reader.GetName(i));
                    if (!reader.IsDBNull(i))
                    {
                        output.Writer.WriteValue(reader.GetValue(i));
                    }
                    output.Writer.WriteEndAttribute();
                }
                await output.Writer.WriteEndElementAsync();
                skip++;
                if (skip % 100000 == 0)
                {
                    _logger.LogInformation(
                        "Records processed for {EntityName}: {Count} ({RecordsPerMinute}/minute)",
                        source.EntityName,
                        skip,
                        Math.Floor(skip / watch.Elapsed.TotalMinutes));
                }
            }
            if (output != null)
            {
                await CloseDocumentWriterAsync(output, cancellationToken);
                output = null;
            }
            watch.Stop();
            _logger.LogInformation("Export ended for {EntityName}. Total records: {TotalRecord}", source.EntityName, skip);
            if (skip > 0)
            {
                _logger.LogInformation(
                    "The file was processed in {ElapsedMinutes} minute(s) and {ElapsedSeconds} seconds.",
                    Math.Floor(watch.Elapsed.TotalMinutes), watch.Elapsed.Seconds);
            }
            return partNames;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while processing {EntityName} query", source.EntityName);
            throw;
        }
    }

    private static async Task CloseDocumentWriterAsync(IXmlOutputPart output, CancellationToken cancellationToken = default)
    {
        await output.Writer.WriteEndElementAsync();
        await output.Writer.WriteEndDocumentAsync();
        await output.Writer.FlushAsync();
        output.Writer.Close();
        await output.PostWriteProcessAsync(cancellationToken);
        output.Close();
    }

    private static async Task<IXmlOutputPart> StartDocumentAsync(IXmlOutputFactory outputFactory, SourceQueryItem source, int part, CancellationToken cancellationToken = default)
    {
        IXmlOutputPart output = await outputFactory.CreateAsync(source, part, cancellationToken);
        output.Open();
        await output.Writer.WriteStartDocumentAsync();
        await output.Writer.WriteStartElementAsync(string.Empty, "Document", string.Empty);
        return output;
    }
}