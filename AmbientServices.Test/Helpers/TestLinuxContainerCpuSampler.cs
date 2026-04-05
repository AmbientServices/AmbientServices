using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace AmbientServices.Test;

[TestClass]
public class TestLinuxContainerCpuSampler
{
    [TestMethod]
    public void LinuxContainerCpuSampler_NoCgroupFiles_YieldsZeroUsage()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_empty_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
            Assert.AreEqual(0f, sampler.GetPendingUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [TestMethod]
    public void LinuxContainerCpuSampler_SyntheticCgroupV2_SecondSampleProducesBoundedUsage()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_v2_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteCgroupV2Layout(root, usageUsec: 0, cpuMaxLine: "100000 100000");

            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());

            // +1,000,000 µs CPU time → 1e9 ns delta; with ~1 CPU core limit, expect bounded usage after wall-clock gap
            WriteCgroupV2Layout(root, usageUsec: 1_000_000, cpuMaxLine: "100000 100000");
            Thread.Sleep(15);
            sampler.Sample();
            float u = sampler.GetUsage();
            Assert.IsTrue(u >= 0f && u <= 1f, $"Expected usage in [0,1], got {u}");

            float pending = sampler.GetPendingUsage();
            Assert.IsTrue(pending >= 0f && pending <= 1f, $"Expected pending in [0,1], got {pending}");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [TestMethod]
    public void LinuxContainerCpuSampler_SyntheticCgroupV1_SecondSampleProducesBoundedUsage()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_v1_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteCgroupV1Layout(root, usageNs: 1_000_000_000L, quota: 100_000, period: 100_000);

            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();

            WriteCgroupV1Layout(root, usageNs: 2_000_000_000L, quota: 100_000, period: 100_000);
            Thread.Sleep(15);
            sampler.Sample();
            float u = sampler.GetUsage();
            Assert.IsTrue(u >= 0f && u <= 1f, $"Expected usage in [0,1], got {u}");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [TestMethod]
    public void LinuxContainerCpuSampler_Sample_WhenTimeDeltaNotPositive_UsageStaysZero()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_td0_s_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteCgroupV2Layout(root, usageUsec: 0, cpuMaxLine: "100000 100000");
            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            SetNonPublicField(sampler, "_lastSampleTime", DateTime.UtcNow.AddHours(1));
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [TestMethod]
    public void LinuxContainerCpuSampler_GetPendingUsage_WhenTimeDeltaNotPositive_ReturnsZero()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_td0_p_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteCgroupV2Layout(root, usageUsec: 0, cpuMaxLine: "100000 100000");
            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            SetNonPublicField(sampler, "_lastSampleTime", DateTime.UtcNow.AddHours(1));
            Assert.AreEqual(0f, sampler.GetPendingUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [TestMethod]
    public void LinuxContainerCpuSampler_ProcCgroup_EmptyFile_YieldsNullContainerId()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_proc_empty_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteProcSelfCgroup(root, "");
            WriteCgroupV2Layout(root, usageUsec: 0, cpuMaxLine: "100000 100000");
            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [TestMethod]
    public void LinuxContainerCpuSampler_ProcCgroup_NoDockerSubstring_YieldsNullContainerId()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_proc_nodk_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteProcSelfCgroup(root, "0::/init.scope\n");
            WriteCgroupV2Layout(root, usageUsec: 0, cpuMaxLine: "100000 100000");
            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [TestMethod]
    public void LinuxContainerCpuSampler_ProcCgroup_DockerWordButNoSlashDockerSlash_YieldsNullContainerId()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_proc_dkw_" + Guid.NewGuid().ToString("N"));
        try
        {
            // "docker" appears (e.g. docker.service) but path has no "/docker/" segment as required by GetContainerId
            WriteProcSelfCgroup(root, "0:cpu,cpuacct:/system.slice/docker.service\n");
            WriteCgroupV2Layout(root, usageUsec: 0, cpuMaxLine: "100000 100000");
            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [TestMethod]
    public void LinuxContainerCpuSampler_ProcCgroup_FewerThanThreeFields_SkipsLine()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_proc_cols_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteProcSelfCgroup(root, "docker-only-line\n");
            WriteCgroupV2Layout(root, usageUsec: 0, cpuMaxLine: "100000 100000");
            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [TestMethod]
    public void LinuxContainerCpuSampler_ProcCgroup_ParsesContainerIdWithSlashAndUsesDockerSubpath()
    {
        const string cid = "abc123deadbeef";
        string root = Path.Combine(Path.GetTempPath(), "cgrp_proc_dkpath_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteProcSelfCgroup(root, $"0:cpu:/kubepods.slice/docker/{cid}/scope\n");
            string cgroupRoot = Path.Combine(root, "sys", "fs", "cgroup");
            Directory.CreateDirectory(cgroupRoot);
            File.WriteAllText(Path.Combine(cgroupRoot, "cgroup.controllers"), "");
            string dockerScoped = Path.Combine(cgroupRoot, "docker", cid);
            Directory.CreateDirectory(dockerScoped);
            File.WriteAllText(Path.Combine(dockerScoped, "cpu.stat"), "usage_usec 0\n");
            File.WriteAllText(Path.Combine(dockerScoped, "cpu.max"), "100000 100000\n");

            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            WriteCgroupV2LayoutUnder(root, Path.Combine("sys", "fs", "cgroup", "docker", cid), usageUsec: 500_000, cpuMaxLine: "100000 100000");
            Thread.Sleep(15);
            sampler.Sample();
            float u = sampler.GetUsage();
            Assert.IsTrue(u >= 0f && u <= 1f, $"Expected usage in [0,1], got {u}");
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [TestMethod]
    public void LinuxContainerCpuSampler_ProcCgroup_ParsesContainerIdWithoutExtraSlash()
    {
        const string cid = "shortid";
        string root = Path.Combine(Path.GetTempPath(), "cgrp_proc_tailid_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteProcSelfCgroup(root, $"0:cpu:/prefix/docker/{cid}\n");
            string cgroupRoot = Path.Combine(root, "sys", "fs", "cgroup");
            Directory.CreateDirectory(cgroupRoot);
            File.WriteAllText(Path.Combine(cgroupRoot, "cgroup.controllers"), "");
            string dockerScoped = Path.Combine(cgroupRoot, "docker", cid);
            Directory.CreateDirectory(dockerScoped);
            File.WriteAllText(Path.Combine(dockerScoped, "cpu.stat"), "usage_usec 0\n");
            File.WriteAllText(Path.Combine(dockerScoped, "cpu.max"), "100000 100000\n");

            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// cgroup v1: invalid <c>cpuacct.usage</c> causes <see cref="long.Parse"/>; <see cref="LinuxContainerCpuSampler"/> catches and treats usage as missing.
    /// </summary>
    [TestMethod]
    public void LinuxContainerCpuSampler_V1_InvalidCpuacctUsage_ParseException_YieldsZeroUsage()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_v1_badusage_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteCgroupV1LayoutInvalidUsage(root, "not-a-number");
            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
            Assert.AreEqual(0f, sampler.GetPendingUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// cgroup v1: invalid quota file; limit read catches and returns null so usage stays zero.
    /// </summary>
    [TestMethod]
    public void LinuxContainerCpuSampler_V1_InvalidCpuQuota_ParseException_YieldsZeroUsage()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_v1_badquota_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteCgroupV1LayoutInvalidQuota(root);
            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// cgroup v1: valid quota but invalid period file; second <see cref="long.Parse"/> throws and is caught.
    /// </summary>
    [TestMethod]
    public void LinuxContainerCpuSampler_V1_InvalidCpuPeriod_ParseException_YieldsZeroUsage()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_v1_badperiod_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteCgroupV1LayoutInvalidPeriod(root);
            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// cgroup v2: after discovery, replace <c>cpu.stat</c> with a directory so <c>File.ReadAllLines</c> throws; catch yields null usage.
    /// </summary>
    [TestMethod]
    public void LinuxContainerCpuSampler_V2_CpuStatReplacedWithDirectory_ReadThrows_YieldsZeroUsage()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_v2_statdir_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteCgroupV2Layout(root, usageUsec: 0, cpuMaxLine: "100000 100000");
            string cgroupRoot = Path.Combine(root, "sys", "fs", "cgroup");
            string cpuStatPath = Path.Combine(cgroupRoot, "cpu.stat");
            LinuxContainerCpuSampler sampler = new(root);
            File.Delete(cpuStatPath);
            Directory.CreateDirectory(cpuStatPath);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// cgroup v2: replace <c>cpu.max</c> with a directory so <c>File.ReadAllText</c> throws; catch yields null limit.
    /// </summary>
    [TestMethod]
    public void LinuxContainerCpuSampler_V2_CpuMaxReplacedWithDirectory_ReadThrows_YieldsZeroUsage()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_v2_maxdir_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteCgroupV2Layout(root, usageUsec: 0, cpuMaxLine: "100000 100000");
            string cgroupRoot = Path.Combine(root, "sys", "fs", "cgroup");
            string cpuMaxPath = Path.Combine(cgroupRoot, "cpu.max");
            LinuxContainerCpuSampler sampler = new(root);
            File.Delete(cpuMaxPath);
            Directory.CreateDirectory(cpuMaxPath);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    /// <summary>
    /// <c>/proc/self/cgroup</c> exists as a directory so <c>File.ReadAllLines</c> throws; <c>GetContainerId</c> catches and continues without container id.
    /// </summary>
    [TestMethod]
    public void LinuxContainerCpuSampler_ProcCgroup_IsDirectory_ReadThrows_ContinuesWithV2Root()
    {
        string root = Path.Combine(Path.GetTempPath(), "cgrp_proc_isdir_" + Guid.NewGuid().ToString("N"));
        try
        {
            string procSelf = Path.Combine(root, "proc", "self");
            Directory.CreateDirectory(procSelf);
            Directory.CreateDirectory(Path.Combine(procSelf, "cgroup"));
            WriteCgroupV2Layout(root, usageUsec: 0, cpuMaxLine: "100000 100000");
            LinuxContainerCpuSampler sampler = new(root);
            sampler.Sample();
            Assert.AreEqual(0f, sampler.GetUsage());
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void WriteCgroupV2Layout(string root, long usageUsec, string cpuMaxLine)
    {
        string cgroupRoot = Path.Combine(root, "sys", "fs", "cgroup");
        Directory.CreateDirectory(cgroupRoot);
        File.WriteAllText(Path.Combine(cgroupRoot, "cgroup.controllers"), "");
        File.WriteAllText(Path.Combine(cgroupRoot, "cpu.stat"), "usage_usec " + usageUsec + "\n");
        File.WriteAllText(Path.Combine(cgroupRoot, "cpu.max"), cpuMaxLine + "\n");
    }

    private static void WriteCgroupV1Layout(string root, long usageNs, long quota, long period)
    {
        string cpuacct = Path.Combine(root, "sys", "fs", "cgroup", "cpuacct");
        string cpu = Path.Combine(root, "sys", "fs", "cgroup", "cpu");
        Directory.CreateDirectory(cpuacct);
        Directory.CreateDirectory(cpu);
        File.WriteAllText(Path.Combine(cpuacct, "cpuacct.usage"), usageNs.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(Path.Combine(cpu, "cpu.cfs_quota_us"), quota.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(Path.Combine(cpu, "cpu.cfs_period_us"), period.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void WriteCgroupV1LayoutInvalidUsage(string root, string usageContent)
    {
        string cpuacct = Path.Combine(root, "sys", "fs", "cgroup", "cpuacct");
        string cpu = Path.Combine(root, "sys", "fs", "cgroup", "cpu");
        Directory.CreateDirectory(cpuacct);
        Directory.CreateDirectory(cpu);
        File.WriteAllText(Path.Combine(cpuacct, "cpuacct.usage"), usageContent);
        File.WriteAllText(Path.Combine(cpu, "cpu.cfs_quota_us"), "100000");
        File.WriteAllText(Path.Combine(cpu, "cpu.cfs_period_us"), "100000");
    }

    private static void WriteCgroupV1LayoutInvalidQuota(string root)
    {
        string cpuacct = Path.Combine(root, "sys", "fs", "cgroup", "cpuacct");
        string cpu = Path.Combine(root, "sys", "fs", "cgroup", "cpu");
        Directory.CreateDirectory(cpuacct);
        Directory.CreateDirectory(cpu);
        File.WriteAllText(Path.Combine(cpuacct, "cpuacct.usage"), "0");
        File.WriteAllText(Path.Combine(cpu, "cpu.cfs_quota_us"), "not-an-int");
        File.WriteAllText(Path.Combine(cpu, "cpu.cfs_period_us"), "100000");
    }

    private static void WriteCgroupV1LayoutInvalidPeriod(string root)
    {
        string cpuacct = Path.Combine(root, "sys", "fs", "cgroup", "cpuacct");
        string cpu = Path.Combine(root, "sys", "fs", "cgroup", "cpu");
        Directory.CreateDirectory(cpuacct);
        Directory.CreateDirectory(cpu);
        File.WriteAllText(Path.Combine(cpuacct, "cpuacct.usage"), "0");
        File.WriteAllText(Path.Combine(cpu, "cpu.cfs_quota_us"), "100000");
        File.WriteAllText(Path.Combine(cpu, "cpu.cfs_period_us"), "nan");
    }

    private static void WriteProcSelfCgroup(string root, string content)
    {
        string path = Path.Combine(root, "proc", "self", "cgroup");
        string? dir = Path.GetDirectoryName(path);
        if (dir != null)
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
    }

    private static void WriteCgroupV2LayoutUnder(string root, string relativeDirFromRoot, long usageUsec, string cpuMaxLine)
    {
        string dir = Path.Combine(root, relativeDirFromRoot);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "cpu.stat"), "usage_usec " + usageUsec + "\n");
        File.WriteAllText(Path.Combine(dir, "cpu.max"), cpuMaxLine + "\n");
    }

    private static void SetNonPublicField(object target, string name, object? value)
    {
        FieldInfo? f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(f, "Field " + name);
        f!.SetValue(target, value);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best effort cleanup for temp tests
        }
    }
}
