using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuestionsApp.Web.DB;
using QuestionsApp.Web.Hubs;

namespace QuestionsApp.Web.Api.Commands
{
    public class VoteForQuestionCommand : IRequestHandler<VoteForQuestionRequest, IResult>
    {
        private readonly QuestionsContext _context;
        private readonly IHubContext<QuestionsHub>? _hub;
        public VoteForQuestionCommand(QuestionsContext context, IHubContext<QuestionsHub>? hub)
        {
            _context = context;
            _hub = hub;
        }

        public async Task<IResult> Handle(VoteForQuestionRequest request, CancellationToken cancellationToken)
        {
            if (!await _context.Questions.AnyAsync(q => q.ID == request.QuestionID, cancellationToken))
                return Results.BadRequest("Invalid Question ID");

            _context.Votes.Add(new VoteDB { QuestionID = request.QuestionID });
            await _context.SaveChangesAsync(cancellationToken);
            await _hub.SendRefreshAsync();
            return Results.Ok();
        }
    }

    public class VoteForQuestionRequest : IRequest<IResult>
    {
        public int QuestionID { get; set; }
    }
}
