using QuestPDF.Fluent;
using QuestPDF.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

// The last two sections of the ata: signature lines and the attachment list.
public partial class AtaPdfService
{
    private static void AddSignatures(ColumnDescriptor column, ref int sectionNumber, MeetingAta ata, bool isCV)
    {
        SectionTitle(column, ref sectionNumber, "Assinaturas");

        var presidentName = ata.PresidentUser != null ? FormatUserName(ata.PresidentUser) : "";

        if (isCV)
        {
            // CV: Presidente do CV (always) + Secretário (only if selected)
            var hasSecretary = ata.FirstSecretaryUser != null;
            var secretaryName = hasSecretary ? FormatUserName(ata.FirstSecretaryUser!) : "";

            column.Item().PaddingTop(30).Row(row =>
            {
                SignatureBlock(row, presidentName, "O Presidente do CV");

                if (hasSecretary)
                {
                    row.ConstantItem(40);
                    SignatureBlock(row, secretaryName, "O Secretário");
                }
            });
        }
        else
        {
            // AG: Presidente da Mesa (always) + 1.º Secretário (if selected) + 2.º Secretário (if selected)
            var hasFirstSec = ata.FirstSecretaryUser != null;
            var hasSecondSec = ata.SecondSecretaryUser != null;
            var firstSecName = hasFirstSec ? FormatUserName(ata.FirstSecretaryUser!) : "";
            var secondSecName = hasSecondSec ? FormatUserName(ata.SecondSecretaryUser!) : "";

            column.Item().PaddingTop(30).Row(row =>
            {
                SignatureBlock(row, presidentName, "O Presidente da Mesa");

                if (hasFirstSec)
                {
                    row.ConstantItem(20);
                    SignatureBlock(row, firstSecName, "O 1.º Secretário");
                }

                if (hasSecondSec)
                {
                    row.ConstantItem(20);
                    SignatureBlock(row, secondSecName, "O 2.º Secretário");
                }
            });
        }
    }

    private static void SignatureBlock(RowDescriptor row, string name, string role)
    {
        row.RelativeItem().Column(sigCol =>
        {
            sigCol.Item().Text(name).FontSize(10).AlignCenter();
            sigCol.Item().BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(5);
            sigCol.Item().Text(role).FontSize(9).AlignCenter();
        });
    }

    // Only called when at least one attachment is flagged IncludeInPdf.
    private static void AddAttachments(ColumnDescriptor column, ref int sectionNumber, MeetingAta ata)
    {
        SectionTitle(column, ref sectionNumber, "Anexos");
        column.Item().PaddingTop(10).Column(attachColumn =>
        {
            foreach (var attachment in ata.Attachments.Where(a => a.IncludeInPdf))
            {
                var attachmentText = attachment.Name;
                if (!string.IsNullOrEmpty(attachment.Description))
                {
                    attachmentText += $" — {attachment.Description}";
                }
                attachColumn.Item().Text($"☑ {attachmentText}").FontSize(10);
            }
        });
    }
}
