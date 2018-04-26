using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace MarkChat.DAL.Repository
{
    public interface IGenericRepository<TEntity> where TEntity : class
    {
        void Add(TEntity item);
        TEntity FindById(object id);
        IEnumerable<TEntity> GetAll();
        IEnumerable<TEntity> FindAll(Expression<Func<TEntity, bool>> predicate);
        void Remove(TEntity item);
        void Update(TEntity item);
        //-----Async
        Task<TEntity> AddAsync(TEntity item);
        Task<TEntity> FindByIdAsync(object id);
        Task<IEnumerable<TEntity>> GetAllAsync();
        Task<IEnumerable<TEntity>> FindAllAsync(Expression<Func<TEntity, bool>> predicate);
        Task<TEntity> RemoveAsync(TEntity item);
        Task<TEntity> UpdateAsync(TEntity updated, object key);
    }
}
