using System.Threading.Tasks;
using Search.Models;

namespace Search.Services
{
    public interface ISearchClient
    {
        SearchSchema Schema { get; }
        Task<GenericSearchResult> SearchAsync(SearchQueryBuilder queryBuilder, string refiner = null);
    }
}