using MediatR;
using Microsoft.AspNetCore.SignalR;
using QuestionsApp.Web.DB;
using QuestionsApp.Web.Hubs;

namespace QuestionsApp.Web.Api.Commands
{
    public class AskQuestionCommand : IRequestHandler<AskQuestionRequest, IResult>
    {
        private readonly QuestionsContext _context;
        private readonly IHubContext<QuestionsHub>? _hub;

        public AskQuestionCommand(QuestionsContext context, IHubContext<QuestionsHub>? hub)
        {
            _context = context;
            _hub = hub;
        }

        public async Task<IResult> Handle(AskQuestionRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return Results.BadRequest("The Question Content can not be empty");

            _context.Questions.Add(new QuestionDB { Content = request.Content });
            await _context.SaveChangesAsync(cancellationToken);

            await _hub.SendRefreshAsync();
            return Results.Ok();
        }
    }

    public class AskQuestionRequest :IRequest<IResult>
    {
        public string Content { get; set; } = "";
    }

}
