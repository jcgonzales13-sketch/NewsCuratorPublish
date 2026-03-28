using AiNewsCurator.Api.Models.Operations;
using AiNewsCurator.Domain.Entities;
using AiNewsCurator.Domain.Enums;

namespace AiNewsCurator.UnitTests;

public sealed class OperationsViewModelTests
{
    [Fact]
    public void OperationsSourceViewModel_Should_Project_EditForm_And_Profile_Label()
    {
        var viewModel = new OperationsSourceViewModel
        {
            Source = new Source
            {
                Name = ".NET Blog",
                Type = SourceType.Rss,
                Url = "https://devblogs.microsoft.com/dotnet/feed/",
                Language = "en",
                IsActive = true,
                Priority = 8,
                MaxItemsPerRun = 10,
                IncludeKeywordsJson = "[\"dotnet\",\"c#\"]",
                ExcludeKeywordsJson = "[\"gaming\"]",
                TagsJson = "[\"official\",\"dotnet\"]"
            }
        };

        Assert.Equal(".NET / C#", viewModel.EditorialProfileLabel);
        Assert.Equal("dotnet", viewModel.EditForm.EditorialProfile);
        Assert.Equal("dotnet, c#", viewModel.EditForm.IncludeKeywords);
        Assert.Equal("gaming", viewModel.EditForm.ExcludeKeywords);
    }

    [Fact]
    public void OperationsNewsItemViewModel_Should_Default_To_GeneralAi_When_No_Curation()
    {
        var viewModel = new OperationsNewsItemViewModel
        {
            NewsItem = new NewsItem
            {
                Title = "Title",
                ImageUrl = null
            }
        };

        Assert.Equal("General AI", viewModel.EditorialProfileLabel);
        Assert.Equal(string.Empty, viewModel.ImageForm.ImageUrl);
    }

    [Fact]
    public void OperationsDraftViewModel_Should_Classify_Auth_Failures_And_Extract_Audit_Text()
    {
        var viewModel = new OperationsDraftViewModel
        {
            Draft = new PostDraft
            {
                PostText = "Draft body",
                ValidationErrorsJson = "[]",
                Status = DraftStatus.Failed
            },
            LatestPublication = new Publication
            {
                Status = PublicationStatus.Failed,
                ErrorMessage = "LinkedIn unauthorized",
                RequestPayload = """
                {
                  "specificContent": {
                    "com.linkedin.ugc.ShareContent": {
                      "shareCommentary": {
                        "text": "Post body sent to LinkedIn"
                      }
                    }
                  }
                }
                """,
                ResponsePayload = """{"message":"Unauthorized"}"""
            }
        };

        Assert.True(viewModel.HasFailedPublication);
        Assert.Equal("Connection issue", viewModel.LatestPublicationFailureCategory);
        Assert.Equal("Validate or refresh LinkedIn credentials before retrying.", viewModel.LatestPublicationFailureGuidance);
        Assert.Equal("LinkedIn unauthorized", viewModel.LatestPublicationFailureSummary);
        Assert.Equal("Post body sent to LinkedIn", viewModel.LatestPublicationSentText);
        Assert.True(viewModel.HasPublicationAudit);
    }
}
