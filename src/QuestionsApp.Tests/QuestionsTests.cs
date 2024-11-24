using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QuestionsApp.Web.Api.Commands;
using QuestionsApp.Web.Api.Queries;
using QuestionsApp.Web.DB;

namespace QuestionsApp.Tests
{
    public class QuestionsTests
    {
        private readonly QuestionsContext _context;

        public QuestionsTests()
        {
            var options = new DbContextOptionsBuilder<QuestionsContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new QuestionsContext(options);
        }

        private GetQuestionsQuery NewGetQuestionsQueryHandler => new(_context);
        private AskQuestionCommand NewAskQuestionCommandHandler => new(_context, null);
        private VoteForQuestionCommand NewVoteForQuestionCommandHandler => new(_context, null);


        [Fact]
        public async Task Empty()
        {
            var response = await NewGetQuestionsQueryHandler.Handle(new GetQuestionsRequest(), default);
            response.Should().BeEmpty();
        }

        [Fact]
        public async Task OneQuestion()
        {
            var askResponse =
                await NewAskQuestionCommandHandler.Handle(new AskQuestionRequest { Content = "Dummy Question" },
                    default);
            askResponse.Should().NotBeNull();

            var response = await NewGetQuestionsQueryHandler.Handle(new GetQuestionsRequest(), default);
            response.Should().HaveCount(1);
        }

        [Fact]
        public async Task OneQuestionAndVote()
        {
            var askResponse =
                await NewAskQuestionCommandHandler.Handle(new AskQuestionRequest { Content = "Dummy Question" },
                    default);
            askResponse.Should().NotBeNull();

            var response = await NewGetQuestionsQueryHandler.Handle(new GetQuestionsRequest(), default);
            response.Should().HaveCount(1);
            response[0].Votes.Should().Be(0);

            var voteResponse =
                await NewVoteForQuestionCommandHandler.Handle(
                    new VoteForQuestionRequest { QuestionID = response[0].ID }, default);
            voteResponse.Should().NotBeNull();

            response = await NewGetQuestionsQueryHandler.Handle(new GetQuestionsRequest(), default);
            response.Should().HaveCount(1);
            response[0].Votes.Should().Be(1);
        }
    }
}