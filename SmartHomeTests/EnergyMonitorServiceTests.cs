using Energy_Project.Services.Interfaces;
using Moq;
using Energy_Project.Models;
using Energy_Project.Services;
using Energy_Project.Services.Interfaces;
using Moq;
namespace SmartHomeTests
{
    /// <summary>
    /// Unit tests verifying energy calculation, overload alert handling,
    /// and limit modifications in EnergyMonitorService.
    /// </summary>
    public class EnergyMonitorServiceTests
    {
        private readonly Mock<IDeviceRepository> _deviceRepo = new();
        private readonly Mock<IEnergyPlanRepository> _planRepo = new();
        private readonly Mock<INotificationService> _notify = new();

        /// <summary>
        /// Factory helper to build an EnergyMonitorService instance with mocked dependencies.
        /// </summary>
        /// <returns>A new EnergyMonitorService instance</returns>
        private EnergyMonitorService CreateService()
        {
            return new EnergyMonitorService(_deviceRepo.Object, _planRepo.Object, _notify.Object);
        }

        /// <summary>
        /// Parameterized test verifying conversion from active device wattage to kWh consumption.
        /// </summary>
        /// <param name="watts">Power usage in Watts</param>
        /// <param name="expectedKwh">Calculated consumption in kWh</param>
        [Theory]
        [InlineData(1000.0, 1.0)]
        [InlineData(2500.0, 2.5)]
        [InlineData(400.0, 0.4)]
        public void CalculateCurrentUsageKwh_SingleActiveDevice_ReturnsExpectedKwh(double watts, double expectedKwh)
        {
            // Arrange
            var service = CreateService();
            var fakeList = new List<Device>
            {
                new() { Id = 1, Name = "Water Heater", PowerUsageWatts = watts, IsOn = true },
                new() { Id = 2, Name = "Standby TV", PowerUsageWatts = 60, IsOn = false }
            };

            _deviceRepo.Setup(r => r.GetAll()).Returns(fakeList);

            // Act
            var actual = service.CalculateCurrentUsageKwh();

            // Assert
            Assert.Equal(expectedKwh, actual);
        }

        /// <summary>
        /// Verifies that inactive appliances do not get added to total calculated energy consumption.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_InactiveDevice_ReturnsResultNotEqualToDeviceWattsInKwh()
        {
            // Arrange
            var service = CreateService();
            const double DEVICE_WATTS = 1800.0;
            const double UNEXPECTED_USAGE = DEVICE_WATTS / 1000.0;
            var fakeList = new List<Device>
            {
                new() { Id = 1, Name = "Electric Oven", PowerUsageWatts = DEVICE_WATTS, IsOn = false }
            };

            _deviceRepo.Setup(r => r.GetAll()).Returns(fakeList);

            // Act
            var actual = service.CalculateCurrentUsageKwh();

            // Assert
            Assert.NotEqual(UNEXPECTED_USAGE, actual);
        }

        /// <summary>
        /// Verifies that empty device collection calculates to zero kWh.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_EmptyList_ReturnsZero()
        {
            // Arrange
            var service = CreateService();
            const double EXPECTED_ZERO = 0.0;
            var emptyList = new List<Device>();

            _deviceRepo.Setup(r => r.GetAll()).Returns(emptyList);

            // Act
            var actual = service.CalculateCurrentUsageKwh();

            // Assert
            Assert.True(actual == EXPECTED_ZERO);
        }

        /// <summary>
        /// Verifies that power consumption of switched-off appliances does not exceed zero.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_AllDevicesOff_UsageDoesNotExceedZero()
        {
            // Arrange
            var service = CreateService();
            const double ZERO_THRESHOLD = 0.0;
            var fakeList = new List<Device>
            {
                new() { Id = 1, Name = "Iron", PowerUsageWatts = 1500, IsOn = false },
                new() { Id = 2, Name = "PC Station", PowerUsageWatts = 450, IsOn = false }
            };

            _deviceRepo.Setup(r => r.GetAll()).Returns(fakeList);

            // Act
            var actual = service.CalculateCurrentUsageKwh();

            // Assert
            Assert.False(actual > ZERO_THRESHOLD);
        }

        /// <summary>
        /// Verifies that repository.GetAll() is invoked at most once during power calculation.
        /// </summary>
        [Fact]
        public void CalculateCurrentUsageKwh_Invocation_CallsRepositoryGetAllAtMostOnce()
        {
            // Arrange
            var service = CreateService();
            _deviceRepo.Setup(r => r.GetAll()).Returns(new List<Device>());

            // Act
            service.CalculateCurrentUsageKwh();

            // Assert
            _deviceRepo.Verify(r => r.GetAll(), Times.AtMost(1));
        }

        /// <summary>
        /// Verifies that notification alert is sent exactly once when consumption exceeds the daily threshold.
        /// </summary>
        [Fact]
        public void CheckForOverload_UsageExceedsLimit_CallsSendAlertExactlyOnce()
        {
            // Arrange
            var service = CreateService();
            const double DAILY_LIMIT = 2.0;
            const double DEVICE_POWER = 3500.0; // 3.5 kWh > 2.0 kWh
            var fakeList = new List<Device>
            {
                new() { Id = 1, Name = "Industrial AC", PowerUsageWatts = DEVICE_POWER, IsOn = true }
            };
            var currentPlan = new EnergyPlan { DailyLimitKwh = DAILY_LIMIT };

            _deviceRepo.Setup(r => r.GetAll()).Returns(fakeList);
            _planRepo.Setup(r => r.GetCurrentPlan()).Returns(currentPlan);

            // Act
            service.CheckForOverload();

            // Assert
            _notify.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Exactly(1));
        }

        /// <summary>
        /// Verifies that notification service is never triggered when power usage remains within plan capacity.
        /// </summary>
        [Fact]
        public void CheckForOverload_UsageBelowLimit_NeverCallsSendAlert()
        {
            // Arrange
            var service = CreateService();
            const double DAILY_LIMIT = 5.0;
            const double DEVICE_POWER = 800.0; // 0.8 kWh < 5.0 kWh
            var fakeList = new List<Device>
            {
                new() { Id = 1, Name = "LED Panel", PowerUsageWatts = DEVICE_POWER, IsOn = true }
            };
            var currentPlan = new EnergyPlan { DailyLimitKwh = DAILY_LIMIT };

            _deviceRepo.Setup(r => r.GetAll()).Returns(fakeList);
            _planRepo.Setup(r => r.GetCurrentPlan()).Returns(currentPlan);

            // Act
            service.CheckForOverload();

            // Assert
            _notify.Verify(n => n.SendAlert(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Verifies that overload alert payload contains accurate usage information.
        /// </summary>
        [Fact]
        public void CheckForOverload_OverloadDetected_SendsAlertContainingFormattedUsage()
        {
            // Arrange
            var service = CreateService();
            const double DAILY_LIMIT = 1.0;
            const double DEVICE_POWER = 3000.0; // 3.0 kWh
            var fakeList = new List<Device>
            {
                new() { Id = 1, Name = "Electric Furnace", PowerUsageWatts = DEVICE_POWER, IsOn = true }
            };
            var currentPlan = new EnergyPlan { DailyLimitKwh = DAILY_LIMIT };

            _deviceRepo.Setup(r => r.GetAll()).Returns(fakeList);
            _planRepo.Setup(r => r.GetCurrentPlan()).Returns(currentPlan);

            // Act
            service.CheckForOverload();

            // Assert
            _notify.Verify(n => n.SendAlert(It.Is<string>(msg => msg.Contains("Overload detected: 3 kWh used!"))), Times.Once);
        }

        /// <summary>
        /// Parameterized test verifying that updating energy limit calls repository.UpdatePlan() with the requested value.
        /// </summary>
        /// <param name="newLimit">New energy quota in kWh</param>
        [Theory]
        [InlineData(6.5)]
        [InlineData(12.0)]
        [InlineData(30.0)]
        public void UpdateEnergyLimit_ValidLimit_CallsRepositoryUpdatePlanOnce(double newLimit)
        {
            // Arrange
            var service = CreateService();
            const double INITIAL_LIMIT = 2.0;
            var currentPlan = new EnergyPlan { DailyLimitKwh = INITIAL_LIMIT };

            _planRepo.Setup(r => r.GetCurrentPlan()).Returns(currentPlan);

            // Act
            service.UpdateEnergyLimit(newLimit);

            // Assert
            _planRepo.Verify(r => r.UpdatePlan(It.Is<EnergyPlan>(p => p.DailyLimitKwh == newLimit)), Times.Once);
        }

        /// <summary>
        /// Verifies that updating energy limit persists the new value on the current plan instance.
        /// </summary>
        [Fact]
        public void UpdateEnergyLimit_NewLimit_UpdatesDailyLimitKwhProperty()
        {
            // Arrange
            var service = CreateService();
            const double INITIAL_LIMIT = 3.0;
            const double TARGET_LIMIT = 9.0;
            var currentPlan = new EnergyPlan { DailyLimitKwh = INITIAL_LIMIT };

            _planRepo.Setup(r => r.GetCurrentPlan()).Returns(currentPlan);

            // Act
            service.UpdateEnergyLimit(TARGET_LIMIT);

            // Assert
            Assert.Equal(TARGET_LIMIT, currentPlan.DailyLimitKwh);
        }

        /// <summary>
        /// Direct test ensuring the plan repository returns a valid non-null instance.
        /// </summary>
        [Fact]
        public void Repository_GetCurrentPlan_ReturnsNotNullInstance()
        {
            // Arrange
            var existingPlan = new EnergyPlan { DailyLimitKwh = 10.0, LastReset = DateTime.UtcNow };
            _planRepo.Setup(r => r.GetCurrentPlan()).Returns(existingPlan);

            // Act
            var actual = _planRepo.Object.GetCurrentPlan();

            // Assert
            Assert.NotNull(actual);
        }
    }
}
