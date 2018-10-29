using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Exceptions;
using Octopus.Client.Model;
using Octopus.Client.Util;

namespace Octopus.Client.Repositories.Async
{
    public interface ITaskRepository : IPaginate<TaskResource>, IGet<TaskResource>, ICreate<TaskResource>, ICanExtendSpaceContext<ITaskRepository>
    {
        Task<TaskResource> ExecuteHealthCheck(string description = null, int timeoutAfterMinutes = 5, int machineTimeoutAfterMinutes = 1, string environmentId = null, string[] machineIds = null, string restrictTo = null, string workerpoolId = null, string[] workerIds = null);
        Task<TaskResource> ExecuteCalamariUpdate(string description = null, string[] machineIds = null);
        Task<TaskResource> ExecuteBackup(string description = null);
        Task<TaskResource> ExecuteTentacleUpgrade(string description = null, string environmentId = null, string[] machineIds = null, string restrictTo = null, string workerpooltId = null, string[] workerIds = null);
        Task<TaskResource> ExecuteAdHocScript(string scriptBody, string[] machineIds = null, string[] environmentIds = null, string[] targetRoles = null, string description = null, string syntax = "PowerShell");
        Task<TaskDetailsResource> GetDetails(TaskResource resource, bool? includeVerboseOutput = null, int? tail = null);
        Task<TaskResource> ExecuteActionTemplate(ActionTemplateResource resource, Dictionary<string, PropertyValueResource> properties, string[] machineIds = null, string[] environmentIds = null, string[] targetRoles = null, string description = null);
        Task<TaskResource> ExecuteCommunityActionTemplatesSynchronisation(string description = null);
        Task<List<TaskResource>> GetAllActive(int pageSize = int.MaxValue);
        Task<string> GetRawOutputLog(TaskResource resource);
        Task Rerun(TaskResource resource);
        Task Cancel(TaskResource resource);
        Task ModifyState(TaskResource resource, TaskState newState, string reason);
        Task<IReadOnlyList<TaskResource>> GetQueuedBehindTasks(TaskResource resource);
        Task WaitForCompletion(TaskResource task, int pollIntervalSeconds = 4, int timeoutAfterMinutes = 0, Action<TaskResource[]> interval = null);
        Task WaitForCompletion(TaskResource[] tasks, int pollIntervalSeconds = 4, int timeoutAfterMinutes = 0, Action<TaskResource[]> interval = null);
        Task WaitForCompletion(TaskResource[] tasks, int pollIntervalSeconds = 4, int timeoutAfterMinutes = 0, Func<TaskResource[], Task> interval = null);
        Task WaitForCompletion(TaskResource[] tasks, int pollIntervalSeconds = 4, TimeSpan? timeoutAfter = null, Func<TaskResource[], Task> interval = null);
    }

    class TaskRepository : MixedScopeBaseRepository<TaskResource>, ITaskRepository
    {
        public TaskRepository(IOctopusAsyncRepository repository)
            : base(repository, "Tasks")
        {
        }

        TaskRepository(IOctopusAsyncRepository repository, SpaceContext spaceContext)
            : base(repository, "Tasks", spaceContext)
        {
        }

        public Task<TaskResource> ExecuteHealthCheck(
            string description = null, int timeoutAfterMinutes = 5, int machineTimeoutAfterMinutes = 1, string environmentId = null, string[] machineIds = null,
            string restrictTo = null, string workerpoolId = null, string[] workerIds = null)
        {
            // Default space enabled -> Creates it in the default space
            // Default space disabled -> Fails
            EnsureSingleSpaceContext();
            var resource = new TaskResource
            {
                Name = BuiltInTasks.Health.Name,
                Description = string.IsNullOrWhiteSpace(description) ? "Manual health check" : description,
                Arguments = new Dictionary<string, object>
                {
                    {BuiltInTasks.Health.Arguments.Timeout, TimeSpan.FromMinutes(timeoutAfterMinutes)},
                    {BuiltInTasks.Health.Arguments.MachineTimeout, TimeSpan.FromMinutes(machineTimeoutAfterMinutes)},
                    {BuiltInTasks.Health.Arguments.EnvironmentId, environmentId},
                    {BuiltInTasks.Health.Arguments.WorkerpoolId, workerpoolId},
                    {BuiltInTasks.Health.Arguments.RestrictedTo, restrictTo},
                    {
                        BuiltInTasks.Health.Arguments.MachineIds,
                        machineIds?.Concat(workerIds ?? new string[0]).ToArray() ?? workerIds
                    }
                }
            };
            return Create(resource);
        }

        public Task<TaskResource> ExecuteCalamariUpdate(string description = null, string[] machineIds = null)
        {
            EnsureSingleSpaceContext();
            var resource = new TaskResource
            {
                Name = BuiltInTasks.UpdateCalamari.Name,
                Description = string.IsNullOrWhiteSpace(description) ? "Manual Calamari update" : description,
                Arguments = new Dictionary<string, object>
                {
                    {BuiltInTasks.UpdateCalamari.Arguments.MachineIds, machineIds}
                }
            };
            return Create(resource);
        }

        public Task<TaskResource> ExecuteBackup(string description = null)
        {
            var resource = new TaskResource
            {
                Name = BuiltInTasks.Backup.Name,
                Description = string.IsNullOrWhiteSpace(description) ? "Manual backup" : description
            };
            return CreateSystemTask(resource);
        }

        public async Task<TaskResource> ExecuteTentacleUpgrade(string description = null, string environmentId = null, string[] machineIds = null, string restrictTo = null, string workerpoolId = null, string[] workerIds = null)
        {
            EnsureSingleSpaceContext();
            var resource = new TaskResource
            {
                Name = BuiltInTasks.Upgrade.Name,
                Description = string.IsNullOrWhiteSpace(description) ? "Manual upgrade" : description,
                Arguments = new Dictionary<string, object>
                {
                    {BuiltInTasks.Upgrade.Arguments.EnvironmentId, environmentId},
                    {BuiltInTasks.Upgrade.Arguments.WorkerpoolId, workerpoolId},
                    {BuiltInTasks.Upgrade.Arguments.RestrictedTo, restrictTo},
                    {
                        BuiltInTasks.Upgrade.Arguments.MachineIds,
                        machineIds?.Concat(workerIds ?? new string[0]).ToArray() ?? workerIds
                    }
                }
            };
            return await Create(resource).ConfigureAwait(false);
        }

        public async Task<TaskResource> ExecuteAdHocScript(string scriptBody, string[] machineIds = null, string[] environmentIds = null, string[] targetRoles = null, string description = null, string syntax = "PowerShell")
        {
            EnsureSingleSpaceContext();
            var resource = new TaskResource
            {
                Name = BuiltInTasks.AdHocScript.Name,
                Description = string.IsNullOrWhiteSpace(description) ? "Run ad-hoc PowerShell script" : description,
                Arguments = new Dictionary<string, object>
                {
                    {BuiltInTasks.AdHocScript.Arguments.EnvironmentIds, environmentIds},
                    {BuiltInTasks.AdHocScript.Arguments.TargetRoles, targetRoles},
                    {BuiltInTasks.AdHocScript.Arguments.MachineIds, machineIds},
                    {BuiltInTasks.AdHocScript.Arguments.ScriptBody, scriptBody},
                    {BuiltInTasks.AdHocScript.Arguments.Syntax, syntax}
                }
            };
            return await Create(resource).ConfigureAwait(false);
        }

        public Task<TaskResource> ExecuteActionTemplate(ActionTemplateResource template, Dictionary<string, PropertyValueResource> properties, string[] machineIds = null,
                                                        string[] environmentIds = null, string[] targetRoles = null, string description = null)
        {
            if (string.IsNullOrEmpty(template?.Id)) throw new ArgumentException("The step template was either null, or has no ID");

            var resource = new TaskResource(){SpaceId = template.SpaceId};
            resource.Name = BuiltInTasks.AdHocScript.Name;
            resource.Description = string.IsNullOrWhiteSpace(description) ? "Run step template: " + template.Name : description;
            resource.Arguments = new Dictionary<string, object>
                {
                    {BuiltInTasks.AdHocScript.Arguments.EnvironmentIds, environmentIds},
                    {BuiltInTasks.AdHocScript.Arguments.TargetRoles, targetRoles},
                    {BuiltInTasks.AdHocScript.Arguments.MachineIds, machineIds},
                    {BuiltInTasks.AdHocScript.Arguments.ActionTemplateId, template.Id},
                    {BuiltInTasks.AdHocScript.Arguments.Properties, properties}
                };
            return Create(resource);
        }

        public Task<TaskResource> ExecuteCommunityActionTemplatesSynchronisation(string description = null)
        {
            // SpaceId always need to be null, use a different Create method to handle that
            var resource = new TaskResource
            {
                Name = BuiltInTasks.SyncCommunityActionTemplates.Name,
                Description = description ?? "Run " + BuiltInTasks.SyncCommunityActionTemplates.Name
            };

            return CreateSystemTask(resource);
        }

        public async Task<TaskDetailsResource> GetDetails(TaskResource resource, bool? includeVerboseOutput = null, int? tail = null)
        {
            var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (includeVerboseOutput.HasValue)
                args.Add("verbose", includeVerboseOutput.Value);

            if (tail.HasValue)
                args.Add("tail", tail.Value);
            var parameters = ParameterHelper.CombineParameters(GetAdditionalQueryParameters(), args);
            return await Client.Get<TaskDetailsResource>(resource.Link("Details"), parameters).ConfigureAwait(false);
        }

        public async Task<string> GetRawOutputLog(TaskResource resource)
        {
            return await Client.Get<string>(resource.Link("Raw"), GetAdditionalQueryParameters()).ConfigureAwait(false);
        }

        public async Task Rerun(TaskResource resource)
        {
            EnsureTaskCanRunInTheCurrentContext(resource);
            await Client.Post(resource.Link("Rerun"), (TaskResource)null).ConfigureAwait(false);
        }

        public async Task Cancel(TaskResource resource)
        {
            EnsureTaskCanRunInTheCurrentContext(resource);
            await Client.Post(resource.Link("Cancel"), (TaskResource)null).ConfigureAwait(false);
        }

        public async Task ModifyState(TaskResource resource, TaskState newState, string reason)
        {
            EnsureTaskCanRunInTheCurrentContext(resource);
            await Client.Post(resource.Link("State"), new { state = newState, reason = reason }).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<TaskResource>> GetQueuedBehindTasks(TaskResource resource)
        {
            return await Client.ListAll<TaskResource>(resource.Link("QueuedBehind"), GetAdditionalQueryParameters()).ConfigureAwait(false);
        }

        public Task WaitForCompletion(TaskResource task, int pollIntervalSeconds = 4, int timeoutAfterMinutes = 0, Action<TaskResource[]> interval = null)
        {
            return WaitForCompletion(new[] { task }, pollIntervalSeconds, timeoutAfterMinutes, interval);
        }

        public Task WaitForCompletion(TaskResource[] tasks, int pollIntervalSeconds = 4, int timeoutAfterMinutes = 0, Action<TaskResource[]> interval = null)
        {
            Func<TaskResource[], Task> taskInterval = null;
            if (interval != null)
                taskInterval = tr => Task.Run(() => interval(tr));

            return WaitForCompletion(tasks, pollIntervalSeconds, timeoutAfterMinutes, taskInterval);
        }

        public Task WaitForCompletion(TaskResource[] tasks, int pollIntervalSeconds = 4, int timeoutAfterMinutes = 0, Func<TaskResource[], Task> interval = null)
            => WaitForCompletion(tasks, pollIntervalSeconds, TimeSpan.FromMinutes(timeoutAfterMinutes), interval);

        public async Task WaitForCompletion(TaskResource[] tasks, int pollIntervalSeconds = 4, TimeSpan? timeoutAfter = null, Func<TaskResource[], Task> interval = null)
        {
            var start = Stopwatch.StartNew();
            if (tasks == null || tasks.Length == 0)
                return;
            var additionalQueryParameters = GetAdditionalQueryParameters();
            while (true)
            {
                var stillRunning = await Task.WhenAll(
                        tasks.Select(t => Client.Get<TaskResource>(t.Link("Self"), additionalQueryParameters))
                    )
                    .ConfigureAwait(false);

                if (interval != null)
                    await interval(stillRunning).ConfigureAwait(false);

                if (stillRunning.All(t => t.IsCompleted))
                    return;

                if (timeoutAfter.HasValue && timeoutAfter > TimeSpan.Zero && start.Elapsed > timeoutAfter)
                {
                    throw new TimeoutException($"One or more tasks did not complete before the timeout was reached. We waited {start.Elapsed:hh\\:mm\\:ss}  for the tasks to complete.");
                }

                await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="pageSize">Number of items per page, setting to less than the total items still retreives all items, but uses multiple requests reducing memory load on the server</param>
        /// <returns></returns>
        public Task<List<TaskResource>> GetAllActive(int pageSize = int.MaxValue) => FindAll(pathParameters: new { active = true, take = pageSize });

        public ITaskRepository UsingContext(SpaceContext spaceContext)
        {
            return new TaskRepository(Repository, spaceContext);
        }

        void EnsureTaskCanRunInTheCurrentContext(TaskResource task)
        {
            if (string.IsNullOrEmpty(task.SpaceId))
                return;
            var spaceContext = GetCurrentSpaceContext();
            
            spaceContext.ApplySpaceSelection(spaceIds =>
            {
                if (!spaceIds.Contains(task.SpaceId))
                {
                    throw new SpaceScopedOperationOutsideOfCurrentSpaceContextException(task.SpaceId, spaceContext);
                }
            }, () => { });
        }

        async Task<TaskResource> CreateSystemTask(TaskResource task)
        {
            return await Client.Create(await Repository.Link(CollectionLinkName).ConfigureAwait(false), task).ConfigureAwait(false);
        }
    }

    public class SpaceScopedOperationOutsideOfCurrentSpaceContextException : Exception
    {
        public SpaceScopedOperationOutsideOfCurrentSpaceContextException(string spaceId, SpaceContext context) 
            : base($"Attempted to perform a space scoped operation within space {spaceId}, but your current space context does not contain that space id. " +
                   $"Current Space Context: {context.ApplySpaceSelection(spaces => string.Join(", ", spaces), () => "all spaces")}")
        {
        }
    }
}