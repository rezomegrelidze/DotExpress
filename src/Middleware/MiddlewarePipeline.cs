using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotExpress.Middleware
{
    public class MiddlewarePipeline
    {
        private readonly List<Func<Func<Task>, Func<Task>>> _middlewares = new List<Func<Func<Task>, Func<Task>>>();

        public void Use(Func<Func<Task>, Func<Task>> middleware)
        {
            _middlewares.Add(middleware);
        }

        public Func<Task> Build(Func<Task> finalHandler)
        {
            Func<Task> pipeline = finalHandler;

            for (int i = _middlewares.Count - 1; i >= 0; i--)
            {
                pipeline = _middlewares[i](pipeline);
            }

            return pipeline;
        }
    }
}