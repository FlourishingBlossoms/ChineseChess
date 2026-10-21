using SocketGameProtocol;

namespace Surver.Controller
{
    internal abstract class BaseController
    {
        protected RequestCode RequestCode;

        public RequestCode GetRequestCode
        {
            get { return RequestCode; }
        }
    }
}