﻿using System;
using System.Linq;
using System.Reflection;
using Ncqrs.Commands;
using Ncqrs.Domain;
using Ncqrs.Domain.Storage;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Ncqrs.CommandHandling.AutoMapping.Actions
{
    /// <summary>
    /// An auto mapped action for a command. It created the object as specified by the mapping.
    /// </summary>
    public class ObjectCreationAction : IAutoMappedCommandAction
    {
        private readonly ICommand _command;
        private readonly IDomainRepository _repository;
        private readonly ObjectCreationCommandInfo _commandInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectCreationAction"/> class.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <param name="command">The command.</param>
        public ObjectCreationAction(IDomainRepository repository, ICommand command)
        {
            Contract.Requires<ArgumentNullException>(repository != null);
            Contract.Requires<ArgumentNullException>(command != null);

            Contract.Ensures(_repository == repository, "The _repository member should be initialized with the given repository parameter.");
            Contract.Ensures(_command == command, "The _command member should be initialized with the given command parameter.");
            Contract.Ensures(_commandInfo != null, "The _commandInfo should be initialized after construct.");

            _repository = repository;
            _command = command;
            _commandInfo = ObjectCreationCommandInfo.CreateFromDirectMethodCommand(command);
        }

        [ContractInvariantMethod]
        private void ContractInvariants()
        {
            Contract.Invariant(_command != null, "The _command member should never be null.");
            Contract.Invariant(_repository != null, "The _repository member should never be null.");
            Contract.Invariant(_commandInfo != null, "The _commandInfo member should never be null.");
        }

        /// <summary>
        /// Executes this action.
        /// </summary>
        public void Execute()
        {
            Contract.Assume(UnitOfWork.Current == null, "There should not exist a UnitOfWork at this point.");

            using (var work = new UnitOfWork(_repository))
            {
                var targetCtor = GetConstructorBasedOnCommand();

                var parameterValues = CommandAutoMappingConfiguration.GetParameterValues(_command, targetCtor.GetParameters());
                targetCtor.Invoke(parameterValues);

                work.Accept();
            }
        }

        private ConstructorInfo GetConstructorBasedOnCommand()
        {
            var aggregateType = _commandInfo.AggregateType;
            var propertiesToMap = CommandAutoMappingConfiguration.GetCommandProperties(_command);
            var ctorQuery = from ctor in aggregateType.GetConstructors()
                            where ctor.GetParameters().Length == propertiesToMap.Count()
                            where ParametersDoMatchPropertiesToMap(ctor.GetParameters(), propertiesToMap)
                            select ctor;

            if (ctorQuery.Count() == 0)
            {
                var message = String.Format("No constructor found with {0} parameters on aggregate root {1}.",
                                            propertiesToMap.Count(), aggregateType.FullName);
                throw new CommandMappingException(message);
            }
            if (ctorQuery.Count() > 1)
            {
                var message = String.Format("Multiple constructors found with {0} parameters on aggregate root {1}.",
                                            propertiesToMap.Count(), aggregateType.FullName);
                throw new CommandMappingException(message);
            }

            return ctorQuery.First();
        }

        // TODO: Remove this duplicate method.
        private Boolean ParametersDoMatchPropertiesToMap(ParameterInfo[] parameterInfo, IEnumerable<PropertyInfo> propertiesToMap)
        {
            var enumerator = propertiesToMap.GetEnumerator();

            for (int i = 0; i < parameterInfo.Length; i++)
            {
                if (!enumerator.MoveNext())
                {
                    return false;
                }

                if (!parameterInfo[i].ParameterType.IsAssignableFrom(enumerator.Current.PropertyType))
                {
                    return false;
                }
            }

            return true;
        }
    }
}