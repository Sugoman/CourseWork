using LearningTrainerShared.Models;
using MediatR;

namespace LearningAPI.Features.Dictionaries.Queries.GetDictionaries
{
    public class GetDictionariesQuery : IRequest<List<Dictionary>>
    {
        public int UserId { get; set; }

        public GetDictionariesQuery(int userId)
        {
            UserId = userId;
        }
    }
}
