﻿using CsvHelper;
using CsvHelper.Configuration;
using Eurofurence.App.Common.DataDiffUtils;
using Eurofurence.App.Domain.Model.Dealers;
using Eurofurence.App.Domain.Model.Fragments;
using Eurofurence.App.Server.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eurofurence.App.Tools.CliToolBox.Importers.DealersDen
{
    public class PackageImporter
    {
        readonly IImageService _imageService;
        readonly IDealerService _dealerService;
        readonly TextWriter _output;

        public PackageImporter(IImageService imageService, IDealerService dealerService, TextWriter output = null)
        {
            _output = output;
            _dealerService = dealerService;
            _imageService = imageService;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public async Task ImportZipPackageAsync(string fileName)
        {
            var importRecords = new List<DealerRecord>();

            using (var fileStream = File.OpenRead(fileName))
            using (var archive = new ZipArchive(fileStream))
            {
                var csvEntry = archive.Entries.Single(a => a.Name.EndsWith(".csv", StringComparison.CurrentCultureIgnoreCase));

                TextReader reader = new StreamReader(csvEntry.Open(), Encoding.GetEncoding(1252));

                var csvReader = new CsvReader(reader);
                csvReader.Configuration.RegisterClassMap<DealerImportRowClassMap>();
                var csvRecords = csvReader.GetRecords<DealerImportRow>().ToList();

                _output?.WriteLine($"Parsed {csvRecords.Count} records from CSV");

                foreach (var record in csvRecords)
                {
                    var dealerRecord = new DealerRecord()
                    {
                        RegistrationNumber = record.RegNo,
                        AttendeeNickname = record.Nickname,
                        AboutTheArtistText = record.AboutTheArtist,
                        AboutTheArtText = record.AboutTheArt,
                        ArtPreviewCaption = record.ArtPreviewCaption,
                        DisplayName = record.DisplayName,
                        ShortDescription = record.ShortDescription,
                        Merchandise = record.Merchandise,
                        AttendsOnThursday = !String.IsNullOrWhiteSpace(record.AttendsThu),
                        AttendsOnFriday = !String.IsNullOrWhiteSpace(record.AttendsFri),
                        AttendsOnSaturday = !String.IsNullOrWhiteSpace(record.AttendsSat),
                        TelegramHandle = record.Telegram,
                        TwitterHandle = record.Twitter
                    };

                    dealerRecord.ArtistImageId = await GetImageIdAsync(archive, $"artist_{record.RegNo}.", $"dealer:artist:{record.RegNo}");
                    dealerRecord.ArtistThumbnailImageId = await GetImageIdAsync(archive, $"thumbnail_{record.RegNo}.", $"dealer:thumbnail:{record.RegNo}");
                    dealerRecord.ArtPreviewImageId = await GetImageIdAsync(archive, $"art_{record.RegNo}.", $"dealer:art:{record.RegNo}");

                    ImportLinks(dealerRecord, record.Website);

                    importRecords.Add(dealerRecord);
                }
            }

            var existingRecords = await _dealerService.FindAllAsync();

            var patch = new PatchDefinition<DealerRecord, DealerRecord>((source, list) =>
                list.SingleOrDefault(a => a.RegistrationNumber == source.RegistrationNumber));

            patch
                .Map(s => s.RegistrationNumber, t => t.RegistrationNumber)
                .Map(s => s.AttendeeNickname, t => t.AttendeeNickname)
                .Map(s => s.AboutTheArtistText, t => t.AboutTheArtistText)
                .Map(s => s.AboutTheArtText, t => t.AboutTheArtText)
                .Map(s => s.ArtPreviewCaption, t => t.ArtPreviewCaption)
                .Map(s => s.DisplayName, t => t.DisplayName)
                .Map(s => s.ShortDescription, t => t.ShortDescription)
                .Map(s => s.Merchandise, t => t.Merchandise)
                .Map(s => s.ArtistImageId, t => t.ArtistImageId)
                .Map(s => s.ArtistThumbnailImageId, t => t.ArtistThumbnailImageId)
                .Map(s => s.ArtPreviewImageId, t => t.ArtPreviewImageId)
                .Map(s => s.TelegramHandle, t => t.TelegramHandle)
                .Map(s => s.TwitterHandle, t => t.TwitterHandle)
                .Map(s => s.AttendsOnThursday, t => t.AttendsOnThursday)
                .Map(s => s.AttendsOnFriday, t => t.AttendsOnFriday)
                .Map(s => s.AttendsOnSaturday, t => t.AttendsOnSaturday)
                .Map(s => s.Links, t => t.Links);

            var diff = patch.Patch(importRecords, existingRecords);
            await _dealerService.ApplyPatchOperationAsync(diff);

            _output?.WriteLine($"Added: {diff.Count(a => a.Action == ActionEnum.Add)}");
            _output?.WriteLine($"Deleted: {diff.Count(a => a.Action == ActionEnum.Delete)}");
            _output?.WriteLine($"Updated: {diff.Count(a => a.Action == ActionEnum.Update)}");
            _output?.WriteLine($"Not Modified: {diff.Count(a => a.Action == ActionEnum.NotModified)}");
        }

        private void ImportLinks(DealerRecord dealerRecord, string websiteUrls)
        {
            if (string.IsNullOrWhiteSpace(websiteUrls)) return;

            var linkFragments = new List<LinkFragment>();
            if (dealerRecord.Links != null)
            {
                linkFragments.AddRange(dealerRecord.Links);
            }

            var sanitizedParts = websiteUrls
                .Replace(" / ", ";")
                .Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in sanitizedParts)
            {
                var assumedUri = part;
                if (part.Length < 10) continue;

                if (!assumedUri.StartsWith("http://", StringComparison.CurrentCultureIgnoreCase) &&
                    !assumedUri.StartsWith("https://", StringComparison.CurrentCultureIgnoreCase))
                {
                    assumedUri = $"http://{part}";
                }

                if (Uri.IsWellFormedUriString(assumedUri, UriKind.Absolute))
                {
                    linkFragments.Add(new LinkFragment()
                    {
                        FragmentType = LinkFragment.FragmentTypeEnum.WebExternal,
                        Target = assumedUri
                    });
                }
            }

            dealerRecord.Links = linkFragments.ToArray();
        }

        async Task<Guid?> GetImageIdAsync(ZipArchive archive, string fileNameStartsWith, string internalReference)
        {
            var imageEntry = archive.Entries.SingleOrDefault(a => a.Name.StartsWith(fileNameStartsWith, StringComparison.CurrentCultureIgnoreCase));

            if (imageEntry != null)
            {
                using (var s = imageEntry.Open())
                using (var br = new BinaryReader(s))
                {
                    var imageByteArray = br.ReadBytes((int)imageEntry.Length);
                    var result = await _imageService.InsertOrUpdateImageAsync(internalReference, imageByteArray);
                    return result;
                }
            }

            return null;
        }
    }

    public sealed class DealerImportRowClassMap : CsvClassMap<DealerImportRow>
    {
        public DealerImportRowClassMap()
        {
            Map(m => m.RegNo).Name("Reg No.");
            Map(m => m.Nickname).Name("Nick");
            Map(m => m.DisplayName).Name("Display Name");
            Map(m => m.Merchandise).Name("Merchandise");
            Map(m => m.ShortDescription).Name("Short Description");
            Map(m => m.AboutTheArtist).Name("About the Artist");
            Map(m => m.AboutTheArt).Name("About the Art");
            Map(m => m.ArtPreviewCaption).Name("Art Preview Caption");
            Map(m => m.Website).Name("Website");
            Map(m => m.Telegram).Name("Telegram");
            Map(m => m.Twitter).Name("Twitter");
            Map(m => m.AttendsThu).Name("Attends Thu");
            Map(m => m.AttendsFri).Name("Attends Fri");
            Map(m => m.AttendsSat).Name("Attends Sat");
        }
    }

    public class DealerImportRow
    {
        public int RegNo { get; set; }
        public string Nickname { get; set; }
        public string DisplayName { get; set; }
        public string Merchandise { get; set; }
        public string ShortDescription { get; set; }
        public string AboutTheArtist { get; set; }
        public string AboutTheArt { get; set; }
        public string ArtPreviewCaption { get; set; }
        public string AttendsThu { get; set; }
        public string AttendsFri { get; set; }
        public string AttendsSat { get; set; }
        public string Website { get; set; }
        public string Telegram { get; set; }
        public string Twitter { get; set; }
    }
}
