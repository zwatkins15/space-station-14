﻿using System.Threading.Tasks;
using Content.Server.GameObjects.Components.Buckle;
using Content.Shared.GameObjects.Components.Buckle;
using Content.Shared.GameObjects.EntitySystems;
using NUnit.Framework;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using StrapComponent = Content.Server.GameObjects.Components.Strap.StrapComponent;

namespace Content.IntegrationTests.Tests
{
    [TestFixture]
    [TestOf(typeof(BuckleComponent))]
    [TestOf(typeof(StrapComponent))]
    public class BuckleTest : ContentIntegrationTest
    {
        [Test]
        public async Task Test()
        {
            var server = StartServerDummyTicker();

            IEntity human = null;
            IEntity chair = null;
            BuckleComponent buckle = null;
            StrapComponent strap = null;

            server.Assert(() =>
            {
                var mapManager = IoCManager.Resolve<IMapManager>();

                mapManager.CreateNewMapEntity(MapId.Nullspace);

                var entityManager = IoCManager.Resolve<IEntityManager>();

                human = entityManager.SpawnEntity("HumanMob_Content", MapCoordinates.Nullspace);
                chair = entityManager.SpawnEntity("ChairWood", MapCoordinates.Nullspace);

                // Default state, unbuckled
                Assert.True(human.TryGetComponent(out buckle));
                Assert.NotNull(buckle);
                Assert.Null(buckle.BuckledTo);
                Assert.False(buckle.Buckled);
                Assert.True(ActionBlockerSystem.CanMove(human));
                Assert.True(ActionBlockerSystem.CanChangeDirection(human));
                Assert.True(EffectBlockerSystem.CanFall(human));

                // Default state, no buckled entities, strap
                Assert.True(chair.TryGetComponent(out strap));
                Assert.NotNull(strap);
                Assert.IsEmpty(strap.BuckledEntities);
                Assert.Zero(strap.OccupiedSize);

                // Side effects of buckling
                Assert.True(buckle.TryBuckle(human, chair));
                Assert.NotNull(buckle.BuckledTo);
                Assert.True(buckle.Buckled);
                Assert.True(((BuckleComponentState) buckle.GetComponentState()).Buckled);
                Assert.False(ActionBlockerSystem.CanMove(human));
                Assert.False(ActionBlockerSystem.CanChangeDirection(human));
                Assert.False(EffectBlockerSystem.CanFall(human));
                Assert.AreEqual(human.Transform.WorldPosition, chair.Transform.WorldPosition);

                // Side effects of buckling for the strap
                Assert.That(strap.BuckledEntities, Does.Contain(human));
                Assert.That(strap.OccupiedSize, Is.EqualTo(buckle.Size));
                Assert.Positive(strap.OccupiedSize);

                // Trying to buckle while already buckled fails
                Assert.False(buckle.TryBuckle(human, chair));

                // Trying to unbuckle too quickly fails
                Assert.False(buckle.TryUnbuckle(human));
                Assert.False(buckle.ToggleBuckle(human, chair));
                Assert.True(buckle.Buckled);
            });

            // Wait enough ticks for the unbuckling cooldown to run out
            server.RunTicks(60);

            server.Assert(() =>
            {
                // Still buckled
                Assert.True(buckle.Buckled);

                // Unbuckle
                Assert.True(buckle.TryUnbuckle(human));
                Assert.Null(buckle.BuckledTo);
                Assert.False(buckle.Buckled);
                Assert.True(ActionBlockerSystem.CanMove(human));
                Assert.True(ActionBlockerSystem.CanChangeDirection(human));
                Assert.True(EffectBlockerSystem.CanFall(human));

                // Unbuckle, strap
                Assert.IsEmpty(strap.BuckledEntities);
                Assert.Zero(strap.OccupiedSize);

                // Re-buckling has no cooldown
                Assert.True(buckle.TryBuckle(human, chair));
                Assert.True(buckle.Buckled);

                // On cooldown
                Assert.False(buckle.TryUnbuckle(human));
                Assert.True(buckle.Buckled);
                Assert.False(buckle.ToggleBuckle(human, chair));
                Assert.True(buckle.Buckled);
                Assert.False(buckle.ToggleBuckle(human, chair));
                Assert.True(buckle.Buckled);
            });

            // Wait enough ticks for the unbuckling cooldown to run out
            server.RunTicks(60);

            server.Assert(() =>
            {
                // Still buckled
                Assert.True(buckle.Buckled);

                // Unbuckle
                Assert.True(buckle.TryUnbuckle(human));
                Assert.False(buckle.Buckled);

                // Move away from the chair
                human.Transform.WorldPosition += (1000, 1000);

                // Out of range
                Assert.False(buckle.TryBuckle(human, chair));
                Assert.False(buckle.TryUnbuckle(human));
                Assert.False(buckle.ToggleBuckle(human, chair));

                // Move near the chair
                human.Transform.WorldPosition = chair.Transform.WorldPosition + (0.5f, 0);

                // In range
                Assert.True(buckle.TryBuckle(human, chair));
                Assert.True(buckle.Buckled);
                Assert.False(buckle.TryUnbuckle(human));
                Assert.True(buckle.Buckled);
                Assert.False(buckle.ToggleBuckle(human, chair));
                Assert.True(buckle.Buckled);

                // Force unbuckle
                Assert.True(buckle.TryUnbuckle(human, true));
                Assert.False(buckle.Buckled);
                Assert.True(ActionBlockerSystem.CanMove(human));
                Assert.True(ActionBlockerSystem.CanChangeDirection(human));
                Assert.True(EffectBlockerSystem.CanFall(human));
            });

            await server.WaitIdleAsync();
        }
    }
}
