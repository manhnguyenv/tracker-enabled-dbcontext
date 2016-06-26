﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrackingCore;
using TrackingCore.Configuration;
using TrackingCore.Events;
using TrackingCore.Interfaces;
using TrackingCore.Models;

namespace TrackerContext
{
    [SuppressMessage("Microsoft.Design", "CA1063:ImplementIDisposableCorrectly",
        Justification = "False positive.  IDisposable is inherited via DbContext.  See http://stackoverflow.com/questions/8925925/code-analysis-ca1063-fires-when-deriving-from-idisposable-and-providing-implemen for details.")]
    public class TrackerContext : DbContext, ITrackerContext
    {
        private readonly CoreTracker _coreTracker;

        private Func<string> _usernameFactory;
        private string _defaultUsername;
        private Action<dynamic> _metadataConfiguration;

        public virtual void ConfigureUsername(Func<string> usernameFactory)
        {
            _usernameFactory = usernameFactory;
        }

        public virtual void ConfigureUsername(string defaultUsername)
        {
            _defaultUsername = defaultUsername;
        }

        public virtual void ConfigureMetadata(Action<dynamic> metadataConfiguration)
        {
            _metadataConfiguration = metadataConfiguration;
        }

        public virtual event EventHandler<DatabaseChangeEventArgs> OnDatabaseChange
        {
            add { _coreTracker.OnDatabaseChange += value; }
            remove { _coreTracker.OnDatabaseChange -= value; }
        }

        #region -- constructor --
        public TrackerContext()
        {
            _coreTracker = new CoreTracker(this);
        }

        public TrackerContext(DbCompiledModel model)
            : base(model)
        {
            _coreTracker = new CoreTracker(this);
        }

        public TrackerContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
            _coreTracker = new CoreTracker(this);
        }

        public TrackerContext(string nameOrConnectionString, DbCompiledModel model)
            : base(nameOrConnectionString, model)
        {
            _coreTracker = new CoreTracker(this);
        }

        public TrackerContext(DbConnection existingConnection, bool contextOwnsConnection)
            : base(existingConnection, contextOwnsConnection)
        {
            _coreTracker = new CoreTracker(this);
        }

        public TrackerContext(DbConnection existingConnection, DbCompiledModel model, bool contextOwnsConnection)
            : base(existingConnection, model, contextOwnsConnection)
        {
            _coreTracker = new CoreTracker(this);
        }

        public TrackerContext(ObjectContext objectContext, bool dbContextOwnsObjectContext)
            : base(objectContext, dbContextOwnsObjectContext)
        {
            _coreTracker = new CoreTracker(this);
        }

        #endregion

        #region -- savechanges -- 

        /// <summary>
        ///     This method saves the model changes to the database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table.
        ///     Always use this method instead of SaveChanges() whenever possible.
        /// </summary>
        /// <param name="userName">Username of the logged in identity</param>
        /// <returns>Returns the number of objects written to the underlying database.</returns>
        public virtual int SaveChanges(object userName)
        {
            if (!GlobalTrackingConfig.Enabled) return base.SaveChanges();

            dynamic metaData = new ExpandoObject();
            _metadataConfiguration?.Invoke(metaData);

            _coreTracker.AuditChanges(userName, metaData);

            IEnumerable<DbEntityEntry> addedEntries = _coreTracker.GetAdditions();
            // Call the original SaveChanges(), which will save both the changes made and the audit records...Note that added entry auditing is still remaining.
            int result = base.SaveChanges();
            //By now., we have got the primary keys of added entries of added entiries because of the call to savechanges.

            _coreTracker.AuditAdditions(userName, addedEntries, metaData);

            //save changes to audit of added entries
            base.SaveChanges();
            return result;
        }

        /// <summary>
        ///     This method saves the model changes to the database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table.
        /// </summary>
        /// <returns>Returns the number of objects written to the underlying database.</returns>
        public override int SaveChanges()
        {
            if (!GlobalTrackingConfig.Enabled) return base.SaveChanges();

            return SaveChanges(_usernameFactory?.Invoke() ?? _defaultUsername);
        }

        #region -- Async --

        /// <summary>
        ///     Asynchronously saves all changes made in this context to the underlying database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table.
        /// </summary>
        /// <param name="userName">Username of the logged in identity</param>
        /// <param name="cancellationToken">
        ///     A System.Threading.CancellationToken to observe while waiting for the task
        ///     to complete.
        /// </param>
        /// <returns>Returns the number of objects written to the underlying database.</returns>
        public virtual async Task<int> SaveChangesAsync(object userName, CancellationToken cancellationToken)
        {
            if (!GlobalTrackingConfig.Enabled) return await base.SaveChangesAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                cancellationToken.ThrowIfCancellationRequested();

            dynamic metadata = new ExpandoObject();
            _metadataConfiguration?.Invoke(metadata);

            _coreTracker.AuditChanges(userName, metadata);

            IEnumerable<DbEntityEntry> addedEntries = _coreTracker.GetAdditions();

            // Call the original SaveChanges(), which will save both the changes made and the audit records...Note that added entry auditing is still remaining.
            int result = await base.SaveChangesAsync(cancellationToken);

            //By now., we have got the primary keys of added entries of added entiries because of the call to savechanges.
            _coreTracker.AuditAdditions(userName, addedEntries, metadata);

            //save changes to audit of added entries
            await base.SaveChangesAsync(cancellationToken);

            return result;
        }

        /// <summary>
        ///     Asynchronously saves all changes made in this context to the underlying database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table.
        ///     Always use this method instead of SaveChangesAsync() whenever possible.
        /// </summary>
        /// <returns>Returns the number of objects written to the underlying database.</returns>
        public virtual async Task<int> SaveChangesAsync(int userId)
        {
            if (!GlobalTrackingConfig.Enabled) return await base.SaveChangesAsync(CancellationToken.None);

            return await SaveChangesAsync(userId, CancellationToken.None);
        }

        /// <summary>
        ///     Asynchronously saves all changes made in this context to the underlying database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table.
        ///     Always use this method instead of SaveChangesAsync() whenever possible.
        /// </summary>
        /// <returns>Returns the number of objects written to the underlying database.</returns>
        public virtual async Task<int> SaveChangesAsync(string userName)
        {
            if (!GlobalTrackingConfig.Enabled) return await base.SaveChangesAsync(CancellationToken.None);

            return await SaveChangesAsync(userName, CancellationToken.None);
        }

        /// <summary>
        ///     Asynchronously saves all changes made in this context to the underlying database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table with null UserName.
        /// </summary>
        /// <returns>
        ///     A task that represents the asynchronous save operation.  The task result
        ///     contains the number of objects written to the underlying database.
        /// </returns>
        public override async Task<int> SaveChangesAsync()
        {
            if (!GlobalTrackingConfig.Enabled) return await base.SaveChangesAsync(CancellationToken.None);

            return await SaveChangesAsync(_usernameFactory?.Invoke() ?? _defaultUsername, CancellationToken.None);
        }

        /// <summary>
        ///     Asynchronously saves all changes made in this context to the underlying database.
        ///     If the tracker for an entity is active, it will also put the old values in tracking table with null UserName.
        /// </summary>
        /// <param name="cancellationToken">
        ///     A System.Threading.CancellationToken to observe while waiting for the task
        ///     to complete.
        /// </param>
        /// <returns>
        ///     A task that represents the asynchronous save operation.  The task result
        ///     contains the number of objects written to the underlying database.
        /// </returns>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (!GlobalTrackingConfig.Enabled) return await base.SaveChangesAsync(cancellationToken);

            return await SaveChangesAsync(_usernameFactory?.Invoke() ?? _defaultUsername, cancellationToken);
        }

        #endregion

        #endregion
    }
}