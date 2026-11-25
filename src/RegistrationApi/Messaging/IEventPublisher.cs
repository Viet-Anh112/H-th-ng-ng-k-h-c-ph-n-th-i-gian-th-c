using Contracts;

namespace RegistrationApi.Messaging;

public interface IEventPublisher
{
    void Publish(RegistrationEvent evt);
}
