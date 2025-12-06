using Function.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Function.Services
{
    public interface INetworkDataService
    {
        void Initialize();
        // 【查】泛型查询，T 代表你要查的表类型
        List<T> GetAll<T>() where T : class, new();

        // 【增】
        void Add<T>(T item) where T : class;

        // 【改】
        void Update<T>(T item) where T : class;

        // 【删】
        void Delete(object item, int id);
    }
}
