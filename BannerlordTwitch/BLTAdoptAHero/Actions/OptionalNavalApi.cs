using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero.Actions
{
    // Keep paid-DLC types out of BLT's assembly references. Bind only to a behavior
    // already installed in this mission; never load NavalDLC on the user's behalf.
    internal sealed class OptionalNavalApi
    {
        private readonly object logic;
        private readonly Type shipType;
        private readonly PropertyInfo team, capacity;
        private readonly MethodInfo ignoreCapacity, desiredCount, findOrigin, addReserved,
            spawnBatch, assignTroops, getTeamAgents, removeReserved, removeOrigin;

        internal OptionalNavalApi(object logic)
        {
            this.logic = logic;
            var type = logic.GetType();
            shipType = type.Assembly.GetType("NavalDLC.Missions.Objects.MissionShip", true);
            var teamAgents = type.Assembly.GetType("NavalDLC.Missions.MissionLogics.NavalTeamAgents", true);
            team = shipType.GetProperty("Team") ?? throw new MissingMemberException(shipType.FullName, "Team");
            capacity = shipType.GetProperty("TotalCrewCapacity") ?? throw new MissingMemberException(shipType.FullName, "TotalCrewCapacity");
            ignoreCapacity = Method(type, "SetIgnoreTroopCapacities", shipType, typeof(bool));
            desiredCount = Method(type, "SetDesiredTroopCountOfShip", shipType, typeof(int));
            findOrigin = Method(type, "FindTroopOrigin", typeof(TeamSideEnum), typeof(Predicate<IAgentOriginBase>));
            addReserved = Method(type, "AddReservedTroopToShip", typeof(IAgentOriginBase), shipType);
            spawnBatch = Method(type, "SpawnNextBatch", typeof(TeamSideEnum), typeof(bool), typeof(MBList<Agent>));
            assignTroops = Method(type, "AssignTroops", typeof(TeamSideEnum), typeof(bool));
            getTeamAgents = Method(type, "GetTeamAgents", typeof(TeamSideEnum), teamAgents.MakeByRefType());
            removeReserved = Method(teamAgents, "RemoveReservedTroopFromShip", typeof(IAgentOriginBase), shipType);
            removeOrigin = Method(teamAgents, "RemoveTroopOriginAux", typeof(IAgentOriginBase));
        }

        private static MethodInfo Method(Type type, string name, params Type[] parameters) =>
            type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, parameters, null) ?? throw new MissingMethodException(type.FullName, name);

        public static OptionalNavalApi TryCreate(Mission mission)
        {
            var behavior = mission?.MissionBehaviors.FirstOrDefault(b =>
                b.GetType().FullName == "NavalDLC.Missions.MissionLogics.NavalAgentsLogic");
            if (behavior == null) return null;
            try { return new OptionalNavalApi(behavior); }
            catch (MemberAccessException) { return null; }
            catch (TypeLoadException) { return null; }
        }

        public bool IsShip(object ship) => shipType.IsInstanceOfType(ship);
        public Team GetTeam(object ship) => (Team)team.GetValue(ship);
        public int GetCapacity(object ship) => (int)capacity.GetValue(ship);
        public void SetIgnoreTroopCapacities(object ship, bool value) => ignoreCapacity.Invoke(logic, new[] { ship, value });
        public void SetDesiredTroopCountOfShip(object ship, int count) => desiredCount.Invoke(logic, new[] { ship, count });
        public IAgentOriginBase FindTroopOrigin(TeamSideEnum side, Predicate<IAgentOriginBase> predicate) =>
            (IAgentOriginBase)findOrigin.Invoke(logic, new object[] { side, predicate });
        public bool AddReservedTroopToShip(IAgentOriginBase origin, object ship) =>
            (bool)addReserved.Invoke(logic, new[] { origin, ship });
        public void SpawnNextBatch(TeamSideEnum side, bool reinforcement, MBList<Agent> agents) =>
            spawnBatch.Invoke(logic, new object[] { side, reinforcement, agents });
        public void AssignTroops(TeamSideEnum side, bool dynamicTraits) =>
            assignTroops.Invoke(logic, new object[] { side, dynamicTraits });

        public Action CreateReservationCleanup(TeamSideEnum side, IAgentOriginBase origin, object ship)
        {
            var args = new object[] { side, null };
            if (!(bool)getTeamAgents.Invoke(logic, args) || args[1] == null) return null;
            var teamAgents = args[1];
            return () =>
            {
                removeReserved.Invoke(teamAgents, new[] { origin, ship });
                removeOrigin.Invoke(teamAgents, new object[] { origin });
            };
        }
    }
}
