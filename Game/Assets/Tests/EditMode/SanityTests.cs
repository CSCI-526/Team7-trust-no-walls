using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using TrustNoWall.Game;

namespace TrustNoWall.Tests
{
    /// <summary>
    /// Sanity tests to verify core bootstrap infrastructure is in place.
    /// </summary>
    public class SanityTests
    {
        [Test]
        public void GameBootstrap_Boot_HasRuntimeInitializeAttribute()
        {
            var method = typeof(GameBootstrap).GetMethod(
                "Boot",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);

            Assert.IsNotNull(method, "GameBootstrap.Boot method should exist");

            var attribute = method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(attribute, "GameBootstrap.Boot should have RuntimeInitializeOnLoadMethodAttribute");
        }
    }
}
